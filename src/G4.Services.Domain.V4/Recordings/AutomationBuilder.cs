using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace G4.Services.Domain.V4.Recordings
{
    // Converts detached recorder models into the same native G4 job structure exported by Stop-Recorder.
    internal static class AutomationBuilder
    {
        #region *** Constants    ***

        // Pixels one wheel notch scrolls when a recorder reports notches without pixel deltas. It matches the
        // Windows WHEEL_DELTA unit, which the Chromium recorder also uses to count notches.
        private const int ScrollNotchPixels = 120;

        // Standalone modifiers have no playback meaning on their own; they only shape shortcut chords.
        private static readonly HashSet<int> ModifierVirtualKeys =
            [0x10, 0x11, 0x12, 0x5B, 0x5C, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5];

        // Recorder modifier names mapped to the driver's sticky-key vocabulary; Shift plays as left Shift.
        private static readonly Dictionary<string, string> ShortcutKeyNames =
            new(comparer: StringComparer.OrdinalIgnoreCase)
        {
            ["Alt"] = "Alt",
            ["Ctrl"] = "Ctrl",
            ["Shift"] = "LShift",
            ["Win"] = "Win"
        };

        // Windows key virtual-key codes mapped to the driver's sided key names. A Windows key pressed and released
        // with no other input plays as the key itself, for example to open Start.
        private static readonly Dictionary<int, string> WindowsKeyNames = new()
        {
            [0x5B] = "LWin",
            [0x5C] = "RWin"
        };

        // Matches where the engine's CLI parser starts a new argument inside a value: whitespace followed by "--" and
        // a name character, the same look-ahead its argument pattern uses.
        private static readonly Regex ArgumentBreakPattern = new(pattern: @"\s+--[\w,/,\.,\$,\*]");

        // Exports with the camel-case flow contract and omits absent authentication fields instead of writing nulls.
        private static readonly JsonSerializerOptions ExportOptions = new(defaults: JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        #endregion

        #region *** Methods      ***

        // Resolves the node an element event was triggered on: the path node the recorder flagged with
        // isTriggerElement, or the first node when none is flagged, which is the target of a target-first chain.
        // Rule conversion and late locator amendments share this lookup, so both read and patch the same node.
        // Pure lookup: returns null for an event without a recorded path or with a non-object node, and never throws.
        internal static JsonObject GetTriggerNode(JsonObject model)
        {
            // An event without a non-empty recorded path names no element, so there is no node to resolve.
            if (model["chain"] is not JsonObject chain || chain["path"] is not JsonArray { Count: > 0 } path)
            {
                return null;
            }

            // Prefer the recorder's explicit flag so the lookup never depends on the chain orientation alone.
            var flagged = path.OfType<JsonObject>().FirstOrDefault(predicate: TestTriggerNode);
            return flagged ?? path[0] as JsonObject;
        }

        // Converts captures with no window exclusions and returns only the flow; null for an empty recording.
        internal static JsonObject NewAutomation(RecordingAuthenticationModel authentication, RecorderCapture[] captures)
        {
            return NewRecording(authentication, captures, excludedProcessIds: [], excludedWindowTitles: []).Automation;
        }

        // Pure conversion keeps output reproducible and independent from recorder transport lifetime.
        // Events from excluded processes or window titles are dropped before grouping, so they never form a job.
        // Returns a null automation and no jobs for an empty recording.
        internal static RecordedAutomation NewRecording(
            RecordingAuthenticationModel authentication,
            RecorderCapture[] captures,
            int[] excludedProcessIds,
            string[] excludedWindowTitles)
        {
            // Resolve the exclusion sets once; zero is never a real process and absent arrays exclude nothing.
            var processIds = (excludedProcessIds ?? []).Where(predicate: processId => processId > 0).ToHashSet();
            var windowTitles = (excludedWindowTitles ?? [])
                .Where(predicate: title => !string.IsNullOrWhiteSpace(value: title))
                .ToArray();

            // Merge by the inner model timestamp; stable sorting retains endpoint and source order for ties.
            var ordered = captures
                .SelectMany(selector: capture => capture.Events.Select(selector: model => new RecordedEvent(
                    Options: capture.Options,
                    Model: model)))
                .Where(predicate: item => !TestExcludedWindow(model: item.Model, processIds, windowTitles))
                .OrderBy(keySelector: item => GetNumber(value: item.Model["timestamp"]))
                .ToArray();

            // Return no automation for an empty recording so callers never receive a flow without jobs.
            if (ordered.Length == 0)
            {
                return new RecordedAutomation(Automation: null, Jobs: []);
            }

            // Consecutive grouping preserves A -> B -> A as three jobs rather than reordering whole recorders.
            var groups = new List<List<RecordedEvent>>();
            foreach (var item in ordered)
            {
                var isNewEndpointRun = groups.Count == 0 || groups[^1][0].Options.BaseUrl != item.Options.BaseUrl;
                if (isNewEndpointRun)
                {
                    groups.Add(item: []);
                }

                groups[^1].Add(item);
            }

            // Only a single-job flow inherits a real automation driver; multi-job flows use per-job drivers.
            var isSingleJob = groups.Count == 1;
            var automationDriver = isSingleJob
                ? CopyDriver(driver: groups[0][0].Options.DriverParameters)
                : new JsonObject { ["driver"] = "NoDriver", ["driverBinaries"] = "." };

            // Track each endpoint's launch job number so later runs against the same endpoint reattach to its driver.
            var jobs = new JsonArray();
            var jobRecorders = new List<RecordingJobModel>();
            var launchJobs = new Dictionary<string, int>();
            for (var index = 0; index < groups.Count; index++)
            {
                // Close an endpoint's driver only when no later run returns to the same endpoint.
                var group = groups[index];
                var options = group[0].Options;
                var jobNumber = index + 1;
                var isEndpointRevisited = groups
                    .Skip(count: jobNumber)
                    .Any(predicate: later => later[0].Options.BaseUrl == options.BaseUrl);
                var isLastForRecorder = !isEndpointRevisited;
                var job = NewJob(group, jobNumber, isLastForRecorder);

                // Reuse the launch job's driver when the event stream returns to an earlier endpoint.
                if (!isSingleJob)
                {
                    var isReturningEndpoint = launchJobs.TryGetValue(key: options.BaseUrl, value: out var launchJob);
                    job["driverParameters"] = isReturningEndpoint
                        ? CopyDriver(driver: new Dictionary<string, object> { ["driver"] = $"Job({launchJob})" })
                        : CopyDriver(driver: options.DriverParameters);
                    launchJobs.TryAdd(key: options.BaseUrl, value: jobNumber);
                }

                // Append the job after driver resolution, and map it to its recorder for per-recorder post-processing.
                jobs.Add(value: job);
                jobRecorders.Add(item: new RecordingJobModel
                {
                    BaseUrl = options.BaseUrl,
                    Id = GetText(value: job["reference"]?["id"]),
                    Index = index
                });
            }

            // Describe the single recorder stage with designer-facing metadata so imported flows show a readable stage.
            var stageDescription = "Stage to execute all actions captured during the recording session.";
            var stageReference = NewReference(
                id: "recorder-stage-01",
                name: "Recorded Actions Stage",
                description: stageDescription);

            // Serialization produces a detached node, so flow readers never share the session's authentication.
            var automation = new JsonObject
            {
                ["authentication"] = JsonSerializer.SerializeToNode(value: authentication, options: ExportOptions),
                ["driverParameters"] = automationDriver,
                ["stages"] = new JsonArray(new JsonObject { ["reference"] = stageReference, ["jobs"] = jobs })
            };

            return new RecordedAutomation(Automation: automation, Jobs: [.. jobRecorders]);
        }

        // Reads optional recorder text that conversion cannot trust: locator metadata and locator amendments come
        // from the browser extension and can be absent or carry another JSON type. Unlike GetText, which fails on a
        // present non-string node, an absent or non-text value resolves to an empty string, so callers only test
        // the length.
        internal static string ResolveText(JsonNode value)
        {
            return value is JsonValue textValue && textValue.TryGetValue(out string text) ? text : "";
        }

        // Converts a recorded pixel value into the invariant text the plugin arguments use. A missing or unusable
        // value counts as zero.
        private static string ConvertToPixelText(JsonNode value) => ConvertToPixelText(pixels: GetNumber(value));

        // Converts a pixel distance into the invariant whole-number text the plugin arguments use; a fraction is
        // rounded away from zero, because pointer moves take whole pixels.
        private static string ConvertToPixelText(double pixels)
        {
            var wholePixels = (long)Math.Round(pixels, MidpointRounding.AwayFromZero);
            return wholePixels.ToString(provider: CultureInfo.InvariantCulture);
        }

        // Copies playback settings and satisfies the designer's driver-plus-binaries import contract.
        // The caller-owned dictionary is never mutated; a non-string driver or binaries value fails through GetText.
        private static JsonObject CopyDriver(Dictionary<string, object> driver)
        {
            // Serialize into a new node so the binaries default lands on the copy rather than on caller-owned settings.
            var copy = JsonSerializer.SerializeToNode(value: driver, options: ExportOptions).AsObject();

            // Default binaries only for a named driver without a location, so designer import resolves it locally.
            var isDriverNamed = !string.IsNullOrEmpty(value: GetText(value: copy["driver"]));
            var isBinariesMissing = isDriverNamed && string.IsNullOrEmpty(value: GetText(value: copy["driverBinaries"]));
            if (isBinariesMissing)
            {
                copy["driverBinaries"] = ".";
            }

            return copy;
        }

        // Numbers arrive from both parsed JSON and in-memory fixtures; culture-invariant conversion accepts either.
        // Absent, unparseable, and non-finite values resolve to zero instead of failing the conversion.
        private static double GetNumber(JsonNode value)
        {
            // Parse through the invariant culture so decimal timestamps read identically on every host locale.
            var text = value?.ToString();
            var isParsed = double.TryParse(
                s: text,
                style: NumberStyles.Float,
                provider: CultureInfo.InvariantCulture,
                result: out var number);

            // Collapse unusable values to zero so ordering and gap arithmetic never observe NaN or infinity.
            var isUsable = isParsed && double.IsFinite(d: number);
            return isUsable ? number : 0;
        }

        // Resolves the modifiers that turn a key release into a shortcut chord; empty for typed text.
        // Current recorders send the chord captured with the press; legacy recorders fall back to the stream.
        private static string[] GetShortcutModifiers(JsonObject model, HashSet<int> heldModifiers, string key)
        {
            // Trust the recorder's press-time chord whenever the payload carries one, including an empty one.
            if (model["value"]?["modifiers"] is JsonArray reported)
            {
                var names = reported.Select(selector: node => GetText(value: node));
                return [.. names.Where(predicate: name => name.Length > 0)];
            }

            // Legacy streams: Control, Alt, or Windows always form a chord, and Shift only with a non-text key,
            // because a typed glyph already carries Shift.
            var isControl = heldModifiers.Overlaps(other: [0x11, 0xA2, 0xA3]);
            var isAlt = heldModifiers.Overlaps(other: [0x12, 0xA4, 0xA5]);
            var isShift = heldModifiers.Overlaps(other: [0x10, 0xA0, 0xA1]);
            var isWindows = heldModifiers.Overlaps(other: [0x5B, 0x5C]);
            var isText = ResolveCharacter(key).Length == 1;
            var isShortcut = isControl || isAlt || isWindows || (isShift && !isText);
            if (!isShortcut)
            {
                return [];
            }

            // Report the held modifiers in the recorder's fixed Ctrl, Alt, Shift, Win order.
            var modifiers = new List<string>();
            if (isControl)
            {
                modifiers.Add(item: "Ctrl");
            }

            if (isAlt)
            {
                modifiers.Add(item: "Alt");
            }

            if (isShift)
            {
                modifiers.Add(item: "Shift");
            }

            if (isWindows)
            {
                modifiers.Add(item: "Win");
            }

            return [.. modifiers];
        }

        // Optional string fields are absent on value-only actions and some desktop element chains.
        // Absent nodes resolve to an empty string; a present non-string node throws InvalidOperationException.
        private static string GetText(JsonNode value) => value?.GetValue<string>() ?? "";

        // Maps Chromium's already-resolved plugin names while preserving each event family's argument contract.
        // Unknown event names pass through as element actions, so new extension plugins keep their locator unchanged.
        private static JsonObject NewChromiumRule(JsonObject model)
        {
            // Resolve the plugin, base rule, and locator once so every event family reads the same normalized values.
            var plugin = GetText(value: model["event"]);
            var rule = NewRule(plugin, model);
            var locator = GetText(value: model["chain"]?["locator"]);
            var value = model["value"];

            // Shape each event family's argument according to the plugin contract it targets.
            switch (plugin)
            {
                case "SendKeys":
                    // Typed text travels through the keys macro against the recorded element.
                    SetLocator(rule, locator);
                    var keysArgument = "{{$ --keys:" + GetText(value: value?["text"]) + "}}";
                    rule["argument"] = keysArgument;
                    break;
                case "OpenUrl":
                    // Navigation carries only the target URL and never an element locator.
                    rule["argument"] = GetText(value: value?["url"]);
                    break;
                case "UpdatePage":
                case "UndoNavigation":
                case "RedoNavigation":
                    // Page-level navigation actions need neither an argument nor a locator.
                    break;
                case "SwitchWindow":
                case "CloseWindow":
                    // Window actions address a tab index; the first window is the fallback when the index is absent.
                    rule["argument"] = value?["index"]?.ToString() ?? "0";
                    break;
                case "InvokeScroll":
                    // The scroll replays by the recorded pixel offsets from the current position.
                    SetLocator(rule, locator);
                    rule["argument"] = NewScrollArgument(value);
                    break;
                case "InvokeDragAndDrop":
                    // The dragged element is the chain target; where the drag ends travels in the argument.
                    SetLocator(rule, locator);
                    rule["argument"] = NewDragArgument(value);
                    break;
                default:
                    // Remaining element actions carry only their target locator.
                    SetLocator(rule, locator);
                    break;
            }

            // A target inside a shadow root is addressed by its shadow path, because the chain locator of such an
            // event only reaches the outermost shadow host.
            SetShadowLocator(rule, model);

            // Chromium paths visit the target first; UIA paths use the opposite orientation.
            SetElementName(rule, model, targetFirst: true);

            // Chromium element rules also carry the markup around their target when the recorder captured it.
            SetDomContext(rule, model);

            // The ranked locator candidates travel with the rule, so a client can swap or heal the chosen locator.
            SetLocatorCandidates(rule, model);

            // A drag and drop also carries the candidates of its drop element, for the same reason.
            SetTargetCandidates(rule, model);
            return rule;
        }

        // Shapes the InvokeDragAndDrop argument from a Chromium drag event's value: the drop element when one has a
        // locator the argument can carry, otherwise the distance the recorder measured from the source center to
        // the release point, which is the offset the plugin moves by. XPath is the plugin's default locator type,
        // so only another type is written.
        private static string NewDragArgument(JsonNode value)
        {
            // Prefer the drop element, because it still holds when the layout moves between recording and replay.
            var (targetLocator, targetLocatorType) = ResolveDragTarget(target: value?["target"] as JsonObject);
            if (targetLocator.Length > 0)
            {
                var locatorTypeParameter = targetLocatorType == "Xpath" ? "" : " --TargetLocator:" + targetLocatorType;
                return "{{$ --Target:" + targetLocator + locatorTypeParameter + "}}";
            }

            // Replay a drag without a usable drop element by the recorded distance.
            var deltaX = ConvertToPixelText(value: value?["deltaX"]);
            var deltaY = ConvertToPixelText(value: value?["deltaY"]);
            return "{{$ --DeltaX:" + deltaX + " --DeltaY:" + deltaY + "}}";
        }

        // Resolves one continuous endpoint run and closes its driver only on the endpoint's last job.
        // The job number and machine name form the designer reference; waits are inserted after CloseBrowser placement.
        private static JsonObject NewJob(List<RecordedEvent> group, int jobNumber, bool isLastForRecorder)
        {
            // Identify the run's endpoint and source machine; an absent machine name keeps the reference id valid.
            var options = group[0].Options;
            var machine = GetText(value: group[0].Model["machineName"]);
            machine = machine.Length == 0 ? "unknown" : machine;

            // Chromium and Android events arrive pre-mapped to plugins, while UIA events need release classification and
            // text runs. Android chains run target first like Chromium's, so they share the Chromium rule builder.
            var isPreMapped = options.IsLaunchable;
            var rules = isPreMapped
                ? group
                    .Where(predicate: item => GetText(value: item.Model["event"]).Length > 0)
                    .Select(selector: item => NewChromiumRule(model: item.Model))
                    .ToList()
                : NewUiaRules(group);

            // Preserve the extension's final CloseBrowser placement on the endpoint's last job only.
            if (isLastForRecorder)
            {
                rules.Add(item: new JsonObject { ["$type"] = "Action", ["pluginName"] = "CloseBrowser" });
            }

            // Insert think-time waits only when the endpoint opted in, preserving the per-job pacing boundary.
            var timing = options.ThinkTimeSettings;
            if (timing.Enabled)
            {
                rules = NewTimedRules(rules, timing);
            }

            // Describe the job with machine-scoped designer metadata so multi-machine flows remain distinguishable.
            var jobId = $"recorded-actions-job-{jobNumber}-{machine.ToLowerInvariant()}";
            var jobName = $"Recorded Actions Job ({machine})";
            var jobDescription = $"Job to execute all recorded actions from machine {machine}.";
            return new JsonObject
            {
                ["reference"] = NewReference(id: jobId, name: jobName, description: jobDescription),
                ["rules"] = new JsonArray(rules.Select(selector: rule => (JsonNode)rule).ToArray())
            };
        }

        // Converts UIA text runs and discrete keys without normalizing the plugin-owned raw key vocabulary.
        // Standard mode targets element patterns, while User32 and Coordinate modes emit physical keyboard input.
        private static JsonObject NewKeyboardRule(RecordedEvent item, string keys, bool isDiscrete)
        {
            // Choose the plugin from key shape and capture mode, keeping discrete keys apart from typed text runs.
            var isStandard = item.Options.Mode == CaptureModes.Standard;
            var plugin = (isDiscrete, isStandard) switch
            {
                (true, true) => "SendKeyboardKey",
                (true, false) => "SendUser32KeyboardKey",
                (false, true) => "SendKeys",
                _ => "SendUser32Keys"
            };

            // Wrap the keys in the plugin macro; discrete keys use the singular Key parameter.
            var keyParameter = isDiscrete ? "Key" : "Keys";
            var keysArgument = "{{$ --" + keyParameter + ":" + keys + "}}";
            var rule = NewRule(plugin, model: item.Model);
            rule["argument"] = keysArgument;

            // Coordinate keyboard actions intentionally target the active desktop focus rather than an element, and
            // SendUser32KeyboardKey sends its key to the focused window in every mode, so neither gets onElement.
            var isFocusTargeted = item.Options.Mode == CaptureModes.Coordinate || plugin == "SendUser32KeyboardKey";
            if (!isFocusTargeted)
            {
                SetLocator(rule, locator: GetText(value: item.Model["chain"]?["locator"]));
            }

            // UIA paths run root-first, so the element name search starts from the far end.
            SetElementName(rule, model: item.Model, targetFirst: false);
            return rule;
        }

        // Collects detached copies of the locator metadata a Chromium trigger node carries: the ranked candidates,
        // the shadow-root path, the strength of the chosen locator, and the locator the recorder started from.
        // Empty arrays and empty or non-text values are left out, and a missing node yields an empty dictionary,
        // so callers create no capabilities for an element without metadata.
        private static Dictionary<string, JsonNode> NewLocatorCandidates(JsonObject properties)
        {
            // A node without properties carries no metadata to copy.
            var candidates = new Dictionary<string, JsonNode>();
            if (properties is null)
            {
                return candidates;
            }

            // Arrays are deep-cloned, because a JSON node has a single parent and the event keeps its own copy.
            if (properties["locators"] is JsonArray { Count: > 0 } locators)
            {
                candidates["locators"] = locators.DeepClone();
            }

            if (properties["shadowPath"] is JsonArray { Count: > 0 } shadowPath)
            {
                candidates["shadowPath"] = shadowPath.DeepClone();
            }

            // Text values are copied only when the recorder reported a non-empty string.
            var locatorStrength = ResolveText(value: properties["locatorStrength"]);
            if (locatorStrength.Length > 0)
            {
                candidates["locatorStrength"] = locatorStrength;
            }

            var originalLocator = ResolveText(value: properties["originalLocator"]);
            if (originalLocator.Length > 0)
            {
                candidates["originalLocator"] = originalLocator;
            }

            return candidates;
        }

        // Maps button releases into element-pattern or physical input actions according to UIA mode.
        // Unrecognized buttons emit a placeholder None action rather than failing the conversion.
        private static JsonObject NewMouseRule(RecordedEvent item)
        {
            // Resolve the released button from the event name's first word, such as "Left" in "Left Up".
            var options = item.Options;
            var model = item.Model;
            var button = GetText(value: model["event"]).Split(separator: ' ')[0].ToLowerInvariant();

            // Standard mode invokes element patterns; other modes emit physical User32 clicks.
            var isStandard = options.Mode == CaptureModes.Standard;
            var plugin = button switch
            {
                "left" => isStandard ? "InvokeClick" : "InvokeUser32Click",
                "middle" => isStandard ? "InvokeMiddleClick" : "InvokeUser32MiddleClick",
                "right" => isStandard ? "InvokeContextClick" : "InvokeUser32ContextClick",
                _ => "None"
            };
            var rule = NewRule(plugin, model);
            var chain = model["chain"];

            // Coordinate mode records absolute desktop positions and never emits an element locator.
            if (options.Mode == CaptureModes.Coordinate)
            {
                var x = model["value"]?["x"]?.ToString() ?? "";
                var y = model["value"]?["y"]?.ToString() ?? "";
                var coordinateArgument = "{{$ --X:" + x + " --Y:" + y + "}}";
                rule["argument"] = coordinateArgument;
            }
            else
            {
                // Prefer the primary locator and fall back to the chain's alternate locator when it is absent.
                var locator = GetText(value: chain?["locator"]);
                var resolvedLocator = locator.Length > 0 ? locator : GetText(value: chain?["fallbackLocator"]);
                SetLocator(rule, locator: resolvedLocator);
            }

            // Preserve current chain offsets and the extension's legacy event-root fallback.
            var isOffsetEnabled = options.Mode == CaptureModes.User32 && options.UseOffset;
            if (isOffsetEnabled)
            {
                var offsetX = GetNumber(value: chain?["mouseOffset"]?["x"]);
                var offsetY = GetNumber(value: chain?["mouseOffset"]?["y"]);

                // Read the legacy root offset only when the chain carries no offset at all.
                var isChainOffsetEmpty = offsetX == 0 && offsetY == 0;
                if (isChainOffsetEmpty)
                {
                    offsetX = GetNumber(value: model["offset"]?["x"]);
                    offsetY = GetNumber(value: model["offset"]?["y"]);
                }

                // Emit offsets only when non-zero so plain element clicks keep the plugin's default click point.
                var isOffsetPresent = offsetX != 0 || offsetY != 0;
                if (isOffsetPresent)
                {
                    var formattedX = offsetX.ToString(provider: CultureInfo.InvariantCulture);
                    var formattedY = offsetY.ToString(provider: CultureInfo.InvariantCulture);
                    var offsetArgument =
                        "{{$ --OffsetX:" + formattedX + " --OffsetY:" + formattedY + " --Alignment:TopLeft}}";
                    rule["argument"] = offsetArgument;
                }
            }

            // UIA paths run root-first, so the element name search starts from the far end.
            SetElementName(rule, model, targetFirst: false);
            return rule;
        }

        // Creates designer-compatible metadata without embedding transport or session identifiers in the flow.
        private static JsonObject NewReference(string id, string name, string description)
        {
            return new JsonObject { ["id"] = id, ["name"] = name, ["description"] = description };
        }

        // Timestamp context is preserved for think-time insertion; absent timestamps remain absent.
        private static JsonObject NewRule(string plugin, JsonObject model)
        {
            // Clone the timestamp into rule context so gap measurement never shares nodes with the source model.
            var context = new JsonObject();
            if (model["timestamp"] is JsonNode timestamp)
            {
                context["timestamp"] = timestamp.DeepClone();
            }

            return new JsonObject { ["$type"] = "Action", ["pluginName"] = plugin, ["context"] = context };
        }

        // Shapes the InvokeScroll argument from a scroll event's value. With --Relative the plugin scrolls by --Left
        // and --Top pixels from the current position, through JavaScript, so the rule replays on web pages only.
        // The Chromium recorder sends the pixel deltas of each single wheel event, which are used as they are. A
        // recorder that sends only a direction and a notch count, such as the Android recorder, scrolls
        // ScrollNotchPixels per notch along that direction; a missing direction scrolls down, and a missing or
        // unusable notch count scrolls one notch.
        private static string NewScrollArgument(JsonNode value)
        {
            // Prefer the recorded deltas, because they carry the exact wheel distance on both axes; an axis whose
            // delta is not a JSON number does not move.
            var deltaX = value?["deltaX"];
            var deltaY = value?["deltaY"];
            var isDeltaXRecorded = TestNumber(value: deltaX);
            var isDeltaYRecorded = TestNumber(value: deltaY);
            if (isDeltaXRecorded || isDeltaYRecorded)
            {
                var deltaLeft = isDeltaXRecorded ? ConvertToPixelText(value: deltaX) : "0";
                var deltaTop = isDeltaYRecorded ? ConvertToPixelText(value: deltaY) : "0";
                return "{{$ --Left:" + deltaLeft + " --Top:" + deltaTop + " --Relative}}";
            }

            // Without deltas, turn the notch count into pixels on the axis of the recorded direction; the sign
            // follows the page axes, so up and left are negative.
            var notches = Math.Max(val1: 1, val2: GetNumber(value: value?["notches"]));
            var distance = notches * ScrollNotchPixels;
            var (left, top) = GetText(value: value?["direction"]).ToUpperInvariant() switch
            {
                "UP" => (0d, -distance),
                "LEFT" => (-distance, 0d),
                "RIGHT" => (distance, 0d),
                _ => (0d, distance)
            };

            return "{{$ --Left:" + ConvertToPixelText(pixels: left) + " --Top:" + ConvertToPixelText(pixels: top) +
                " --Relative}}";
        }

        // Plays a shortcut as one sticky chord: every modifier is held, the key is pressed, then all are released
        // in reverse order. User32 input is used in every UIA mode, since element patterns cannot hold a modifier
        // and cannot press a lone Windows key, which is played through this rule with no modifiers.
        private static JsonObject NewShortcutRule(RecordedEvent item, string key, string[] modifiers)
        {
            // Map modifiers to driver names and use the upper-case letter the driver's scan-code map expects.
            var keyName = key.Length == 1 ? key.ToUpperInvariant() : key;
            var modifierNames = modifiers.Select(selector: name =>
                ShortcutKeyNames.GetValueOrDefault(key: name, defaultValue: name));
            var keyParameters = modifierNames.Append(element: keyName).Select(selector: name => "--Key:" + name);
            var chordArgument = "{{$ " + string.Join(separator: ' ', values: keyParameters) + " --Sticky}}";
            var rule = NewRule(plugin: "SendUser32KeyboardKey", model: item.Model);
            rule["argument"] = chordArgument;

            // The plugin presses the chord in the focused window in every mode, so the rule carries no onElement
            // and, without one, no element name.
            return rule;
        }

        // Inserts waits only for gaps above the threshold and caps each duration, matching the existing exporter.
        // Returns a new list; the input rules are reused by reference and never reordered.
        private static List<JsonObject> NewTimedRules(List<JsonObject> rules, ThinkTimeModel timing)
        {
            // Collect into a new list so the caller's rule sequence stays unchanged when waits are inserted.
            var output = new List<JsonObject>();
            for (var index = 0; index < rules.Count; index++)
            {
                // Keep every original rule in order; waits are only ever inserted between neighboring rules.
                output.Add(item: rules[index]);

                // CloseBrowser has no timestamp and never causes an additional synthetic wait.
                var currentTimestamp = rules[index]["context"]?["timestamp"];
                var nextTimestamp = index + 1 < rules.Count ? rules[index + 1]["context"]?["timestamp"] : null;
                var isGapMeasurable = currentTimestamp is not null && nextTimestamp is not null;
                if (!isGapMeasurable)
                {
                    continue;
                }

                // Insert a capped wait only above the threshold so brief pauses do not slow playback.
                var gap = GetNumber(value: nextTimestamp) - GetNumber(value: currentTimestamp);
                if (gap > timing.MinThinkTime)
                {
                    var duration = Math.Min(val1: gap, val2: timing.MaxThinkTime);
                    var formattedDuration = duration.ToString(provider: CultureInfo.InvariantCulture);
                    var roundedSeconds = Math.Round(value: duration / 1000, digits: 2);
                    var seconds = roundedSeconds.ToString(provider: CultureInfo.InvariantCulture);
                    var timeoutArgument = "{{$ --Timeout:" + formattedDuration + "}}";
                    var displayName = $"Think Time ({seconds} seconds)";

                    // Append the wait after the current rule so it pauses playback before the next action.
                    output.Add(item: new JsonObject
                    {
                        ["$type"] = "Action",
                        ["pluginName"] = "WaitFlow",
                        ["argument"] = timeoutArgument,
                        ["capabilities"] = new JsonObject { ["displayName"] = displayName }
                    });
                }
            }

            return output;
        }

        // Maps a UIA left-button release that carries drag facts to InvokeUser32DragAndDrop, the only drag plugin of
        // the UIA driver, in every capture mode, because UI Automation has no drag pattern. Each end is an element
        // point when its element has a usable locator, and a screen point otherwise:
        // - Start: the press-time chain in onElement with its grab offset, or --X and --Y, the press point.
        // - End: --Target with its drop offset; without a usable target, --DeltaX and --DeltaY from the press point
        //   when the start is an element, so the drop follows the element when the window moves, or --TargetX and
        //   --TargetY, the release point, when the start is a screen point too.
        // Coordinate mode records screen positions, so both of its ends are screen points and the rule names no
        // locator. Offsets are measured from the top-left corner of each element and are written with the TopLeft
        // alignment, which every UIA driver version applies. An element the recorder had no bounds for carries no
        // offset and keeps the plugin's center alignment.
        private static JsonObject NewUiaDragRule(RecordedEvent item)
        {
            // Read the release once; TestUiaDrag already proved that the drag facts are an object.
            var model = item.Model;
            var value = model["value"];
            var drag = value["drag"].AsObject();
            var rule = NewRule(plugin: "InvokeUser32DragAndDrop", model);
            var isLocatorMode = item.Options.Mode != CaptureModes.Coordinate;

            // Resolve both elements as a click does: the primary locator, or the alternate one when it is empty.
            // Coordinate mode uses neither, and the target must also fit in the argument.
            var sourceLocator = isLocatorMode ? ResolveChainLocator(chain: model["chain"]) : "";
            var targetLocator = isLocatorMode ? ResolveChainLocator(chain: drag["target"]) : "";
            var isSourceElement = sourceLocator.Length > 0;
            var isTargetElement = targetLocator.Length > 0 && TestArgumentValue(value: targetLocator);

            // Start on the dragged element at its grab point, or at the press point on the screen.
            var parameters = new List<string>();
            if (isSourceElement)
            {
                SetLocator(rule, locator: sourceLocator);
                AddOffsetParameters(
                    parameters,
                    prefix: "",
                    offsetX: drag["sourceOffsetX"],
                    offsetY: drag["sourceOffsetY"]);
            }
            else
            {
                parameters.Add(item: "--X:" + ConvertToPixelText(value: drag["startX"]));
                parameters.Add(item: "--Y:" + ConvertToPixelText(value: drag["startY"]));
            }

            // End on the drop element at its drop point, by the distance from an element start, or at the release
            // point on the screen.
            if (isTargetElement)
            {
                parameters.Add(item: "--Target:" + targetLocator);
                AddOffsetParameters(
                    parameters,
                    prefix: "Target",
                    offsetX: drag["targetOffsetX"],
                    offsetY: drag["targetOffsetY"]);
            }
            else if (isSourceElement)
            {
                var deltaX = GetNumber(value: value["x"]) - GetNumber(value: drag["startX"]);
                var deltaY = GetNumber(value: value["y"]) - GetNumber(value: drag["startY"]);
                parameters.Add(item: "--DeltaX:" + ConvertToPixelText(pixels: deltaX));
                parameters.Add(item: "--DeltaY:" + ConvertToPixelText(pixels: deltaY));
            }
            else
            {
                parameters.Add(item: "--TargetX:" + ConvertToPixelText(value: value["x"]));
                parameters.Add(item: "--TargetY:" + ConvertToPixelText(value: value["y"]));
            }

            // Wrap the parameters in the plugin macro and name the rule after the dragged element, when it has one.
            rule["argument"] = "{{$ " + string.Join(separator: ' ', values: parameters) + "}}";
            SetElementName(rule, model, targetFirst: false);
            return rule;

            // Adds the corner alignment and an offset pair when the recorder measured both offsets; an absent or
            // non-numeric pair leaves the plugin's center alignment, so a zero offset still means the corner itself.
            static void AddOffsetParameters(List<string> parameters, string prefix, JsonNode offsetX, JsonNode offsetY)
            {
                var isMeasured = TestNumber(value: offsetX) && TestNumber(value: offsetY);
                if (!isMeasured)
                {
                    return;
                }

                parameters.Add(item: "--" + prefix + "Alignment:TopLeft");
                parameters.Add(item: "--" + prefix + "OffsetX:" + ConvertToPixelText(value: offsetX));
                parameters.Add(item: "--" + prefix + "OffsetY:" + ConvertToPixelText(value: offsetY));
            }
        }

        // Consumes low-level UIA events while leaving mouse releases available after each typed-character run.
        // The loop index advances inside text runs, so each consumed keyboard event produces at most one rule.
        // Modifier presses and releases are tracked across the stream so legacy recorders still yield chords.
        private static List<JsonObject> NewUiaRules(List<RecordedEvent> group)
        {
            // Collect playable rules in event order; filtered and absorbed events contribute nothing.
            var rules = new List<JsonObject>();
            var heldModifiers = new HashSet<int>();
            var loneWindowsKey = 0;
            for (var index = 0; index < group.Count; index++)
            {
                // Track modifiers and a pending lone Windows press before filtering, because presses never become
                // actions themselves; the release test reads the state left by the events before this one.
                var item = group[index];
                var isLoneWindowsRelease = TestLoneWindowsRelease(loneWindowsKey, model: item.Model);
                loneWindowsKey = ResolveLoneWindowsKey(loneWindowsKey, model: item.Model);
                SetHeldModifier(heldModifiers, model: item.Model);

                // A Windows key released with no other input in between plays as the key itself.
                if (isLoneWindowsRelease)
                {
                    var virtualKey = (int)GetNumber(value: item.Model["value"]?["virtualKey"]);
                    rules.Add(item: NewShortcutRule(item, key: WindowsKeyNames[virtualKey], modifiers: []));
                    continue;
                }

                // Skip presses, modifiers, and chain-less clicks so only playable releases become actions.
                if (!TestUiaEvent(model: item.Model))
                {
                    continue;
                }

                // Mouse releases map one-to-one to actions: a drag and drop when the release carries drag facts,
                // otherwise a click.
                var isMouse = GetText(value: item.Model["type"]).Contains(
                    value: "mouse",
                    comparisonType: StringComparison.OrdinalIgnoreCase);
                if (isMouse)
                {
                    var mouseRule = TestUiaDrag(model: item.Model) ? NewUiaDragRule(item) : NewMouseRule(item);
                    rules.Add(item: mouseRule);
                    continue;
                }

                // Shortcuts become one sticky chord, so Ctrl+A selects instead of typing the letter.
                var rawKey = GetText(value: item.Model["value"]?["key"]);
                var modifiers = GetShortcutModifiers(model: item.Model, heldModifiers, key: rawKey);
                if (modifiers.Length > 0)
                {
                    rules.Add(item: NewShortcutRule(item, key: rawKey, modifiers));
                    continue;
                }

                // Discrete keys remain separate actions; word-spelled Space participates in character runs.
                var key = ResolveCharacter(key: rawKey);
                if (key.Length > 1)
                {
                    rules.Add(item: NewKeyboardRule(item, keys: rawKey, isDiscrete: true));
                    continue;
                }

                // Absorb following character releases into one text run until a non-keyboard event or discrete key.
                var text = key;
                var textEvent = item;
                while (index + 1 < group.Count)
                {
                    var next = group[index + 1];
                    var nextType = GetText(value: next.Model["type"]);
                    var isNextKeyboard = nextType.Contains(
                        value: "keyboard",
                        comparisonType: StringComparison.OrdinalIgnoreCase);
                    if (!isNextKeyboard)
                    {
                        break;
                    }

                    // A lone Windows release ends the run, so the outer loop plays it as its own action.
                    if (TestLoneWindowsRelease(loneWindowsKey, model: next.Model))
                    {
                        break;
                    }

                    // Step over filtered keyboard events, such as presses and modifiers, without ending the run.
                    if (!TestUiaEvent(model: next.Model))
                    {
                        loneWindowsKey = ResolveLoneWindowsKey(loneWindowsKey, model: next.Model);
                        SetHeldModifier(heldModifiers, model: next.Model);
                        index++;
                        continue;
                    }

                    // A discrete key or a shortcut ends the run and is emitted by the outer loop as its own action.
                    var nextRawKey = GetText(value: next.Model["value"]?["key"]);
                    var nextKey = ResolveCharacter(key: nextRawKey);
                    var nextModifiers = GetShortcutModifiers(model: next.Model, heldModifiers, key: nextRawKey);
                    if (nextKey.Length > 1 || nextModifiers.Length > 0)
                    {
                        break;
                    }

                    // Consume the character; a targeted event owns the run's locator over an untargeted one.
                    index++;
                    text += nextKey;
                    var isTargeted = TestChain(model: next.Model) || !TestChain(model: textEvent.Model);
                    textEvent = isTargeted ? next : textEvent;
                }

                // Attribute the combined text to its last targeted character event, matching the extension's
                // locator ownership; an untargeted run plays into whatever element has focus.
                if (text.Length > 0)
                {
                    rules.Add(item: NewKeyboardRule(item: textEvent, keys: text, isDiscrete: false));
                }
            }

            return rules;
        }

        // Resolves the locator of a UIA chain as a click does: the primary locator, or the alternate one when the
        // primary is empty. A missing chain or non-text values resolve to an empty string. Never throws.
        private static string ResolveChainLocator(JsonNode chain)
        {
            var locator = ResolveText(value: chain?["locator"]);
            return locator.Length > 0 ? locator : ResolveText(value: chain?["fallbackLocator"]);
        }

        // Word aliases are classification aids only; discrete driver key normalization remains downstream.
        private static string ResolveCharacter(string key) =>
            key.Equals(value: "space", comparisonType: StringComparison.OrdinalIgnoreCase) ? " " : key;

        // Resolves where a Chromium drag and drop ends, as the locator of the drop element and its locator type, from
        // the target chain the recorder sends in value.target. Candidates are tried in order: the trigger node's
        // usable shadow path, the chain's best XPath, then the other ranked candidates that are not Broken. The
        // first one the rule argument can carry wins. Returns an empty locator when there is no target or no
        // candidate fits, so the caller replays the drag by its distance instead. Never throws.
        private static (string Locator, string LocatorType) ResolveDragTarget(JsonObject target)
        {
            // A drag without a target chain ends where the pointer was released, so it has no drop element.
            if (target?["chain"] is not JsonObject chain)
            {
                return ("", "");
            }

            // Collect the candidates in preference order; the shadow path is the only one that reaches a drop
            // element inside a shadow root, so it comes first.
            var properties = GetTriggerNode(model: target)?["properties"] as JsonObject;
            var candidates = new List<(string Locator, string LocatorType)>
            {
                (ResolveShadowLocator(properties), "CssSelector"),
                (ResolveText(value: chain["locator"]), "Xpath")
            };

            // Append the other ranked candidates the recorder did not find broken, in the recorder's order.
            var rankedLocators = properties?["locators"] as JsonArray ?? [];
            foreach (var candidate in rankedLocators.OfType<JsonObject>())
            {
                var isBroken = ResolveText(value: candidate["verdict"]) == "Broken";
                var locatorType = ResolveText(value: candidate["using"]);
                if (!isBroken && locatorType.Length > 0)
                {
                    candidates.Add(item: (ResolveText(value: candidate["value"]), locatorType));
                }
            }

            // Take the first candidate with a value the argument parser keeps whole.
            var resolved = candidates.FirstOrDefault(predicate: candidate =>
                candidate.Locator.Length > 0 && TestArgumentValue(value: candidate.Locator));
            return resolved.Locator is null ? ("", "") : resolved;
        }

        // Tracks a Windows key pressed with no other input. Its press arms the candidate, and auto-repeat keeps it
        // armed; any other key or mouse press turns it into part of a chord; its own release disarms it.
        // Returns the armed virtual-key code, or zero when no lone Windows press is pending.
        private static int ResolveLoneWindowsKey(int loneWindowsKey, JsonObject model)
        {
            // Read the transition once; a missing key code resolves to zero, which is never a Windows key.
            var isKeyboard = GetText(value: model["type"]).Contains(
                value: "keyboard",
                comparisonType: StringComparison.OrdinalIgnoreCase);
            var isPress = GetText(value: model["event"]).Contains(
                value: "down",
                comparisonType: StringComparison.OrdinalIgnoreCase);
            var virtualKey = (int)GetNumber(value: model["value"]?["virtualKey"]);

            // A release disarms only the armed Windows key; every other release leaves the candidate unchanged.
            if (!isPress)
            {
                var isArmedRelease = isKeyboard && virtualKey == loneWindowsKey;
                return isArmedRelease ? 0 : loneWindowsKey;
            }

            // A Windows press arms the candidate; any other press, keyboard or mouse, clears it.
            var isWindowsPress = isKeyboard && WindowsKeyNames.ContainsKey(key: virtualKey);
            return isWindowsPress ? virtualKey : 0;
        }

        // Resolves the first usable shadow-path candidate of a Chromium trigger node: the first candidate in list
        // order whose strategy is ShadowPath, whose locator type is CssSelector, whose value is a non-empty string,
        // and whose verdict is not Broken, because a broken shadow path falls back to the host XPath. Returns an
        // empty string when the node has none. Never throws: non-object entries and mistyped members are skipped.
        private static string ResolveShadowLocator(JsonObject properties)
        {
            // An absent or mistyped candidate list names no shadow path.
            if (properties?["locators"] is not JsonArray locators)
            {
                return "";
            }

            // Visit the candidates in the recorder's order; entries that are not objects cannot describe a locator.
            foreach (var candidate in locators.OfType<JsonObject>())
            {
                // Read every member as optional text, so an absent or mistyped member fails its comparison.
                var isShadowPath = ResolveText(value: candidate["strategy"]) == "ShadowPath";
                var isCssSelector = ResolveText(value: candidate["using"]) == "CssSelector";
                var isBroken = ResolveText(value: candidate["verdict"]) == "Broken";
                var shadowLocator = ResolveText(value: candidate["value"]);

                // The first usable candidate wins, so later ones are never read.
                var isUsable = isShadowPath && isCssSelector && !isBroken && shadowLocator.Length > 0;
                if (isUsable)
                {
                    return shadowLocator;
                }
            }

            return "";
        }

        // Carries the Chromium trigger node's Base64 DOM context into the rule capabilities, next to the element name,
        // so a client that reads the flow can heal or optimise the locator. Rules without an element, a recorded path, or
        // a text context are left untouched; the decoded length travels along only when it is a positive number.
        private static void SetDomContext(JsonObject rule, JsonObject model)
        {
            // Only element-targeted rules with a recorded path describe an element whose surroundings matter.
            var isElementRule = rule["onElement"] is not null;
            if (!isElementRule || model["chain"]?["path"] is not JsonArray { Count: > 0 } path)
            {
                return;
            }

            // Read the context from the trigger node; a missing, empty, or non-text value means none was captured.
            var properties = path[0]?["properties"];
            var isContextText = properties?["domContext"] is JsonValue contextValue
                && contextValue.TryGetValue(out string domContext)
                && domContext.Length > 0;
            if (!isContextText)
            {
                return;
            }

            // Reuse the element-name capabilities when present, otherwise attach a new capabilities object.
            if (rule["capabilities"] is not JsonObject capabilities)
            {
                capabilities = [];
                rule["capabilities"] = capabilities;
            }

            // Copy the context verbatim, and its decoded byte count when the recorder reported a positive number.
            capabilities["domContext"] = GetText(value: properties["domContext"]);
            var domContextLength = (long)GetNumber(value: properties["domContextLength"]);
            if (domContextLength > 0)
            {
                capabilities["domContextLength"] = domContextLength;
            }
        }

        // Walks from the target outward and carries the first named element verbatim into designer capabilities.
        // Replaces any existing capabilities object on the rule; rules without a name are left untouched.
        private static void SetElementName(JsonObject rule, JsonObject model, bool targetFirst)
        {
            // Name only element-targeted rules with a recorded path, so page and coordinate actions stay unnamed.
            var isElementRule = rule["onElement"] is not null;
            if (!isElementRule || model["chain"]?["path"] is not JsonArray path)
            {
                return;
            }

            // Orient the path target-first, then take the first non-blank name.
            var orderedPath = targetFirst ? path.AsEnumerable() : path.Reverse();
            var names = orderedPath.Select(selector: node => GetText(value: node?["name"]));
            var name = names.FirstOrDefault(predicate: value => !string.IsNullOrWhiteSpace(value));

            // Attach the name only when one exists so unnamed elements carry no empty capability.
            if (name is not null)
            {
                rule["capabilities"] = new JsonObject { ["elementName"] = name };
            }
        }

        // Records a modifier press or release so later key releases know which modifiers are held.
        // Non-keyboard events and non-modifier keys leave the held set unchanged.
        private static void SetHeldModifier(HashSet<int> heldModifiers, JsonObject model)
        {
            // Only keyboard modifier transitions change the held set.
            var isKeyboard = GetText(value: model["type"]).Contains(
                value: "keyboard",
                comparisonType: StringComparison.OrdinalIgnoreCase);
            if (!isKeyboard || !TestModifierKey(model))
            {
                return;
            }

            // Add on press and remove on release, keyed by the sided virtual-key code.
            var virtualKey = (int)GetNumber(value: model["value"]?["virtualKey"]);
            var eventName = GetText(value: model["event"]);
            var isPress = eventName.Contains(value: "down", comparisonType: StringComparison.OrdinalIgnoreCase);
            if (isPress)
            {
                heldModifiers.Add(item: virtualKey);
            }
            else
            {
                heldModifiers.Remove(item: virtualKey);
            }
        }

        // Omits absent element targets rather than serializing explicit null locators into the automation.
        private static void SetLocator(JsonObject rule, string locator)
        {
            // Write the target only when one was recorded so page-level actions keep no onElement field.
            if (locator.Length > 0)
            {
                rule["onElement"] = locator;
            }
        }

        // Carries the Chromium trigger node's locator metadata into the rule capabilities, next to the element name
        // and the DOM context: the ranked candidates, the strength of the chosen locator, the shadow-root path, and
        // the locator the recorder started from. This helper never changes the rule's onElement or its locator type:
        // the chain's best XPath stays, read by the engine as XPath, unless SetShadowLocator retargeted the rule at
        // a shadow path. Rules without an element, a trigger node, or any of these properties are left untouched,
        // so they serialize exactly as before; empty values are never emitted.
        private static void SetLocatorCandidates(JsonObject rule, JsonObject model)
        {
            // Only element-targeted rules describe an element whose alternative locators matter.
            var isElementRule = rule["onElement"] is not null;
            if (!isElementRule)
            {
                return;
            }

            // Collect detached copies first, so an event with no usable metadata never creates an empty capabilities
            // object and the rule never shares nodes with the buffered event.
            var candidates = NewLocatorCandidates(properties: GetTriggerNode(model)?["properties"] as JsonObject);

            // Leave the rule as it is when the trigger node carried none of the locator properties.
            if (candidates.Count == 0)
            {
                return;
            }

            // Reuse the capabilities created for the element name or DOM context, otherwise attach a new object.
            if (rule["capabilities"] is not JsonObject capabilities)
            {
                capabilities = [];
                rule["capabilities"] = capabilities;
            }

            // Write the collected values beside the existing capabilities without replacing any of them.
            foreach (var (key, value) in candidates)
            {
                capabilities[key] = value;
            }
        }

        // Retargets a Chromium element rule at a target inside a shadow root, open or closed. For such a target the
        // chain locator is only the XPath of the outermost shadow host, kept so an older Hub still replays against
        // the host, while the trigger node's ranked candidates carry one entry that reaches the real target: a
        // CssSelector whose segments are chained with ">>>", which the engine resolves one shadow root per segment.
        // Selection rule: the first candidate in list order whose strategy is ShadowPath, whose locator type is
        // CssSelector, whose value is a non-empty string, and whose verdict is not Broken. That value replaces the
        // rule's onElement and the rule names the CssSelector locator type. Rules without an element, a trigger
        // node, or such a candidate are left untouched, so they keep the chain XPath and no locator type. Never
        // throws: non-object entries and absent or mistyped members are skipped, and the event is never mutated.
        private static void SetShadowLocator(JsonObject rule, JsonObject model)
        {
            // Only element-targeted rules carry a locator to replace, so page-level rules never gain a target here.
            var isElementRule = rule["onElement"] is not null;
            if (!isElementRule || GetTriggerNode(model)?["properties"] is not JsonObject properties)
            {
                return;
            }

            // A trigger node without a usable shadow-path candidate keeps the chain locator.
            var shadowLocator = ResolveShadowLocator(properties);
            if (shadowLocator.Length == 0)
            {
                return;
            }

            // Address the real target and name the locator type, because the engine defaults to XPath.
            rule["onElement"] = shadowLocator;
            rule["locator"] = "CssSelector";
        }

        // Carries the locator metadata of a Chromium drag and drop's drop element into capabilities.target: the
        // element name and the same candidates SetLocatorCandidates copies for the dragged element. A client can
        // then swap or heal the drop locator as it does the onElement one. Rules without an element, events
        // without a target chain, and targets without a name or locator metadata are left untouched.
        private static void SetTargetCandidates(JsonObject rule, JsonObject model)
        {
            // Only an element rule whose event names a drop element has a target to describe.
            var isElementRule = rule["onElement"] is not null;
            if (!isElementRule || model["value"]?["target"] is not JsonObject target)
            {
                return;
            }

            // Collect the drop element's candidates as detached copies, so the rule never shares event nodes.
            var targetProperties = GetTriggerNode(model: target)?["properties"] as JsonObject;
            var candidates = NewLocatorCandidates(properties: targetProperties);

            // Name the drop element after the first named node of its target-first path, as the source is named.
            var targetPath = target["chain"]?["path"] as JsonArray ?? [];
            var names = targetPath.Select(selector: node => ResolveText(value: node?["name"]));
            var name = names.FirstOrDefault(predicate: value => !string.IsNullOrWhiteSpace(value));
            if (name is not null)
            {
                candidates["elementName"] = name;
            }

            // Leave the rule as it is when the drop element carried nothing worth keeping.
            if (candidates.Count == 0)
            {
                return;
            }

            // Reuse the capabilities written for the dragged element, otherwise attach a new object.
            if (rule["capabilities"] is not JsonObject capabilities)
            {
                capabilities = [];
                rule["capabilities"] = capabilities;
            }

            capabilities["target"] = new JsonObject(candidates);
        }

        // Tests whether a locator can travel as one value of a plugin argument. The engine's CLI parser starts a new
        // argument at whitespace followed by "--" and a name character, and reads "{{$" as a nested expression, so
        // a locator containing either would be cut or expanded on replay. Pure predicate.
        private static bool TestArgumentValue(string value)
        {
            var isNestedExpression = value.Contains(value: "{{$", comparisonType: StringComparison.Ordinal);
            return !isNestedExpression && !ArgumentBreakPattern.IsMatch(input: value);
        }

        // Tests whether an event carries a non-empty element chain.
        private static bool TestChain(JsonObject model) => model["chain"]?["path"] is JsonArray { Count: > 0 };

        // Tests whether an event happened in an excluded window, matching the VS Code recorder's self-window rule:
        // the top window's process, then any chain node's process, then a window-title fragment as a fallback.
        // Chromium events carry neither process identifiers nor a top window, so they never match.
        private static bool TestExcludedWindow(JsonObject model, HashSet<int> processIds, string[] windowTitles)
        {
            // Read the chain once; an event without one cannot name a window and is never excluded.
            var chain = model["chain"];
            if (chain is null)
            {
                return false;
            }

            // Primary signal: the process that owns the top-level window the interaction happened in.
            var topWindowProcessId = (int)GetNumber(value: chain["topWindow"]?["processId"]);
            if (processIds.Contains(item: topWindowProcessId))
            {
                return true;
            }

            // Secondary signal: any element of the chain owned by an excluded process.
            var path = chain["path"] as JsonArray ?? [];
            var nodeProcessIds = path.Select(selector: node => (int)GetNumber(value: node?["processId"]));
            var isExcludedNode = nodeProcessIds.Any(predicate: processIds.Contains);
            if (isExcludedNode)
            {
                return true;
            }

            // Fallback signal: a title fragment in the top window name, or in the locator when the name is absent.
            var topWindowName = chain["topWindow"]?["name"]?.ToString() ?? "";
            var windowText = topWindowName.Length > 0 ? topWindowName : chain["locator"]?.ToString() ?? "";
            return windowTitles.Any(predicate: title =>
                windowText.Contains(value: title, comparisonType: StringComparison.OrdinalIgnoreCase));
        }

        // Tests whether an event releases the armed lone Windows key. Pure predicate: zero means none is armed.
        private static bool TestLoneWindowsRelease(int loneWindowsKey, JsonObject model)
        {
            // Nothing is pending, so no release can be lone.
            if (loneWindowsKey == 0)
            {
                return false;
            }

            // Match a keyboard release of the same sided Windows key that armed the candidate.
            var isKeyboard = GetText(value: model["type"]).Contains(
                value: "keyboard",
                comparisonType: StringComparison.OrdinalIgnoreCase);
            var eventName = GetText(value: model["event"]);
            var isPress = eventName.Contains(value: "down", comparisonType: StringComparison.OrdinalIgnoreCase);
            var isRelease = eventName.Contains(value: "up", comparisonType: StringComparison.OrdinalIgnoreCase);
            var virtualKey = (int)GetNumber(value: model["value"]?["virtualKey"]);
            return isKeyboard && isRelease && !isPress && virtualKey == loneWindowsKey;
        }

        // Identifies standalone modifier releases by virtual-key code.
        // A missing code resolves to zero, which never matches a modifier.
        private static bool TestModifierKey(JsonObject model)
        {
            // Compare the numeric code so localized key names never affect modifier detection.
            var virtualKey = (int)GetNumber(value: model["value"]?["virtualKey"]);
            return ModifierVirtualKeys.Contains(item: virtualKey);
        }

        // Tests whether a recorded value is a JSON number, which is how the recorders send a measured offset or a
        // scroll delta. Pure predicate: an absent node, a string, or any other JSON type never matches.
        private static bool TestNumber(JsonNode value)
        {
            return value is JsonValue number && number.GetValueKind() == JsonValueKind.Number;
        }

        // Tests whether a path node carries the recorder's trigger flag as a JSON true.
        // Pure predicate: a missing flag or a non-boolean value never matches.
        private static bool TestTriggerNode(JsonObject node)
        {
            return node["isTriggerElement"] is JsonValue flag && flag.TryGetValue(out bool isTrigger) && isTrigger;
        }

        // Tests whether a UIA release is a drag and drop to replay: a left-button release whose value carries the
        // drag facts object the UIA recorder adds past the Windows drag threshold, in any capture mode. Pure
        // predicate.
        private static bool TestUiaDrag(JsonObject model)
        {
            var isLeftRelease = GetText(value: model["event"]).Equals(
                value: "Left Up",
                comparisonType: StringComparison.OrdinalIgnoreCase);
            var isDrag = model["value"]?["drag"] is JsonObject;
            return isLeftRelease && isDrag;
        }

        // Matches the extension's release-only classifier. Mouse clicks need an element chain to target, while
        // keyboard releases and drags are kept without one: the plugins type into the focused element when no
        // locator is set, and a drag replays between screen points.
        // Pure predicate: reads the model without mutation and treats missing fields as non-matching.
        private static bool TestUiaEvent(JsonObject model)
        {
            // Accept only release events.
            var eventName = GetText(value: model["event"]);
            var isPress = eventName.Contains(value: "down", comparisonType: StringComparison.OrdinalIgnoreCase);
            var isRelease = eventName.Contains(value: "up", comparisonType: StringComparison.OrdinalIgnoreCase);
            if (isPress || !isRelease)
            {
                return false;
            }

            // Drop standalone modifier keys, which have no playback meaning on their own.
            var isKeyboard = GetText(value: model["type"]).Contains(
                value: "keyboard",
                comparisonType: StringComparison.OrdinalIgnoreCase);
            if (isKeyboard)
            {
                return !TestModifierKey(model);
            }

            // Keep a click only when it names the element it targets; a drag is kept without one, because it can
            // replay between screen points.
            return TestChain(model) || TestUiaDrag(model);
        }

        #endregion

        #region *** Nested Types ***

        /// <summary>
        /// Links each detached model to its originating configuration during ordered conversion.
        /// </summary>
        /// <remarks>The positional record preserves the pair while sorting without mutating either source.</remarks>
        /// <param name="Options">Configuration that determines this event's playback driver and capture mode.</param>
        /// <param name="Model">Detached event JSON retained for chronology and rule conversion.</param>
        private sealed record RecordedEvent(RecorderModel Options, JsonObject Model);

        #endregion
    }
}
