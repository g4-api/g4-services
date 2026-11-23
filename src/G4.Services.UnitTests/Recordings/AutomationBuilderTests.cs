using G4.Services.Domain.V4.Recordings;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace G4.Services.UnitTests.Recordings
{
    [TestClass]
    [TestCategory("AutomationBuilder")]
    [TestCategory("UnitTest")]
    public sealed class AutomationBuilderTests
    {
        [TestMethod(DisplayName = "Verify Chromium arguments, element labels, capped pacing, " +
            "and single driver inheritance.")]
        public void ChromiumArgumentsAndThinkTimeTest()
        {
            // Arrange: one Chromium run contains text and scroll events separated by a gap above the pacing cap.
            var options = new RecorderModel
            {
                BaseUrl = "http://localhost:9956",
                DriverParameters = new Dictionary<string, object> { ["driver"] = "ChromeDriver", ["customField"] = "retained" },
                ThinkTimeSettings = new ThinkTimeModel { Enabled = true, MinThinkTime = 100, MaxThinkTime = 500 }
            };
            var text = NewEvent(eventName: "SendKeys", timestamp: 100, value: new JsonObject { ["text"] = "hello" });
            var scrollValue = new JsonObject { ["direction"] = "Down", ["notches"] = 3 };
            var scroll = NewEvent(eventName: "InvokeScroll", timestamp: 1100, value: scrollValue);

            // Act: convert a detached capture without invoking a recorder transport.
            var automation = AutomationBuilder.NewAutomation(
                authentication: new RecordingAuthenticationModel { Token = "" },
                captures: [new RecorderCapture(Options: options, Events: [text, scroll])]);

            // Assert: the driver is inherited once, capabilities survive, and the wait uses the configured ceiling.
            var job = automation["stages"][0]["jobs"][0];
            var rules = job["rules"].AsArray();
            Assert.IsNull(value: job["driverParameters"]);
            Assert.AreEqual(expected: "retained", actual: automation["driverParameters"]["customField"].GetValue<string>());
            Assert.AreEqual(expected: ".", actual: automation["driverParameters"]["driverBinaries"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --keys:hello}}", actual: rules[0]["argument"].GetValue<string>());
            Assert.AreEqual(expected: "target", actual: rules[0]["capabilities"]["elementName"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --Timeout:500}}", actual: rules[1]["argument"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --direction:Down --times:3}}", actual: rules[2]["argument"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify Android events convert through the pre-mapped path with locators, arguments and context.")]
        public void AndroidCaptureTest()
        {
            // Arrange: a tap with a view context, typed text, a scroll gesture, a long press, and two page-level events
            // whose chains carry no locator and no path, as the Android recorder app sends them.
            var options = new RecorderModel
            {
                BaseUrl = "http://localhost:9958",
                DriverParameters = new Dictionary<string, object> { ["driver"] = "AndroidDriver" }
            };
            var click = NewEvent(eventName: "InvokeClick", timestamp: 1, value: new JsonObject());
            click["chain"]["path"][0]["properties"] = new JsonObject { ["domContext"] = "PGEvPg==", ["domContextLength"] = 4 };
            var text = NewEvent(eventName: "SendKeys", timestamp: 2, value: new JsonObject { ["text"] = "abc" });
            var scroll = NewEvent(eventName: "InvokeScroll", timestamp: 3, value: new JsonObject { ["direction"] = "Down", ["notches"] = 3 });
            var longClick = NewEvent(eventName: "InvokeLongClick", timestamp: 4, value: new JsonObject { ["durationMilliseconds"] = 500 });
            var activity = NewEvent(eventName: "SwitchActivity", timestamp: 5, value: new JsonObject { ["package"] = "com.app", ["activity"] = "com.app.Home" });
            activity["chain"] = new JsonObject { ["locator"] = "", ["path"] = new JsonArray() };
            var hideKeyboard = NewEvent(eventName: "HideSoftKeyboard", timestamp: 6, value: new JsonObject());
            hideKeyboard["chain"] = new JsonObject { ["locator"] = "", ["path"] = new JsonArray() };

            // Act: convert the capture into a flow.
            JsonObject[] events = [click, text, scroll, longClick, activity, hideKeyboard];
            var automation = AutomationBuilder.NewAutomation(
                authentication: new RecordingAuthenticationModel { Token = "" },
                captures: [new RecorderCapture(Options: options, Events: events)]);
            var rules = automation["stages"][0]["jobs"][0]["rules"].AsArray();
            var plugins = rules.Select(selector: rule => rule["pluginName"].GetValue<string>()).ToArray();

            // Assert: event names pass through as plugins and the endpoint's last job closes the session.
            string[] expected = ["InvokeClick", "SendKeys", "InvokeScroll", "InvokeLongClick", "SwitchActivity", "HideSoftKeyboard", "CloseBrowser"];
            Assert.AreSequenceEqual(expected: expected, actual: plugins);

            // Assert: element events keep their locator and arguments; page-level events target nothing.
            Assert.AreEqual(expected: "//target", actual: rules[0]["onElement"].GetValue<string>());
            Assert.AreEqual(expected: "PGEvPg==", actual: rules[0]["capabilities"]["domContext"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --keys:abc}}", actual: rules[1]["argument"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --direction:Down --times:3}}", actual: rules[2]["argument"].GetValue<string>());
            Assert.AreEqual(expected: "//target", actual: rules[3]["onElement"].GetValue<string>());
            Assert.IsNull(value: rules[4]["onElement"]);
            Assert.IsNull(value: rules[5]["onElement"]);
        }

        [TestMethod(DisplayName = "Verify Chromium element rules carry the trigger node DOM context into their capabilities.")]
        public void ChromiumDomContextTest()
        {
            // Arrange: a named click and an unnamed field carry a context, a third click carries a non-text one, and a
            // navigation carries one on a rule that targets no element.
            var options = new RecorderModel
            {
                BaseUrl = "http://localhost:9956",
                DriverParameters = new Dictionary<string, object> { ["driver"] = "ChromeDriver" }
            };
            var namedClick = NewEvent(eventName: "InvokeClick", timestamp: 1, value: new JsonObject());
            namedClick["chain"]["path"][0]["properties"] = new JsonObject { ["domContext"] = "PGEvPg==", ["domContextLength"] = 4 };

            var unnamedText = NewEvent(eventName: "SendKeys", timestamp: 2, value: new JsonObject { ["text"] = "x" });
            unnamedText["chain"]["path"][0] = new JsonObject
            {
                ["name"] = "",
                ["properties"] = new JsonObject { ["domContext"] = "PGkvPg==" }
            };

            var numberContextClick = NewEvent(eventName: "InvokeClick", timestamp: 3, value: new JsonObject());
            numberContextClick["chain"]["path"][0]["properties"] = new JsonObject { ["domContext"] = 5 };

            var navigation = NewEvent(eventName: "OpenUrl", timestamp: 4, value: new JsonObject { ["url"] = "https://example.com" });
            navigation["chain"]["path"][0]["properties"] = new JsonObject { ["domContext"] = "PGgvPg==" };

            // Act: convert the capture into a flow.
            JsonObject[] events = [namedClick, unnamedText, numberContextClick, navigation];
            var automation = AutomationBuilder.NewAutomation(
                authentication: new RecordingAuthenticationModel { Token = "" },
                captures: [new RecorderCapture(Options: options, Events: events)]);
            var rules = automation["stages"][0]["jobs"][0]["rules"].AsArray();
            var openUrl = rules.First(predicate: rule => rule["pluginName"].GetValue<string>() == "OpenUrl");

            // Assert: the context joins the element name, stands alone without one, and never reaches non-element rules.
            Assert.AreEqual(expected: "target", actual: rules[0]["capabilities"]["elementName"].GetValue<string>());
            Assert.AreEqual(expected: "PGEvPg==", actual: rules[0]["capabilities"]["domContext"].GetValue<string>());
            Assert.AreEqual(expected: 4, actual: rules[0]["capabilities"]["domContextLength"].GetValue<long>());
            Assert.AreEqual(expected: "PGkvPg==", actual: rules[1]["capabilities"]["domContext"].GetValue<string>());
            Assert.IsNull(value: rules[1]["capabilities"]["elementName"]);
            Assert.IsNull(value: rules[1]["capabilities"]["domContextLength"]);
            Assert.AreEqual(expected: "target", actual: rules[2]["capabilities"]["elementName"].GetValue<string>());
            Assert.IsNull(value: rules[2]["capabilities"]["domContext"]);
            Assert.IsNull(value: openUrl["capabilities"]);
        }

        [TestMethod(DisplayName = "Verify Chromium element rules without usable locator properties " +
            "serialize exactly as before.")]
        public void ChromiumLocatorCandidatesAbsentTest()
        {
            // Arrange: the baseline click carries no properties, and three variants carry unrelated, empty, and
            // mistyped locator properties, all on the same timestamp so only the properties can change the rule.
            var baseline = NewEvent(eventName: "InvokeClick", timestamp: 1, value: new JsonObject());
            var unrelated = NewEvent(eventName: "InvokeClick", timestamp: 1, value: new JsonObject());
            unrelated["chain"]["path"][0]["properties"] = new JsonObject
            {
                ["tagName"] = "button",
                ["cssSelector"] = "button.save",
                ["locatorId"] = "5b0c6f0e-0000-4000-8000-000000000001"
            };
            var empty = NewEvent(eventName: "InvokeClick", timestamp: 1, value: new JsonObject());
            empty["chain"]["path"][0]["properties"] = new JsonObject
            {
                ["locators"] = new JsonArray(),
                ["locatorStrength"] = "",
                ["shadowPath"] = new JsonArray(),
                ["originalLocator"] = ""
            };
            var mistyped = NewEvent(eventName: "InvokeClick", timestamp: 1, value: new JsonObject());
            mistyped["chain"]["path"][0]["properties"] = new JsonObject
            {
                ["locators"] = "//button",
                ["locatorStrength"] = 3,
                ["shadowPath"] = new JsonObject(),
                ["originalLocator"] = new JsonArray()
            };

            // Arrange: a navigation carries the complete locator contract on a rule that targets no element.
            var navigationValue = new JsonObject { ["url"] = "https://example.com" };
            var navigation = NewEvent(eventName: "OpenUrl", timestamp: 1, value: navigationValue);
            navigation["chain"]["path"][0]["properties"] = NewLocatorProperties();

            // Act: convert every event on its own so each rule can be compared with the baseline rule.
            var expected = NewChromiumRules(events: [baseline])[0].ToJsonString();
            var unrelatedRule = NewChromiumRules(events: [unrelated])[0];
            var emptyRule = NewChromiumRules(events: [empty])[0];
            var mistypedRule = NewChromiumRules(events: [mistyped])[0];
            var navigationRule = NewChromiumRules(events: [navigation])[0];

            // Assert: no variant adds a capability, so each rule keeps the baseline text and its element name.
            var elementName = unrelatedRule["capabilities"]["elementName"].GetValue<string>();
            Assert.AreEqual(expected: expected, actual: unrelatedRule.ToJsonString());
            Assert.AreEqual(expected: expected, actual: emptyRule.ToJsonString());
            Assert.AreEqual(expected: expected, actual: mistypedRule.ToJsonString());
            Assert.AreEqual(expected: "target", actual: elementName);

            // Assert: locator properties never reach a rule that targets no element.
            Assert.IsNull(value: navigationRule["onElement"]);
            Assert.IsNull(value: navigationRule["capabilities"]);
        }

        [TestMethod(DisplayName = "Verify Chromium element rules carry the trigger node locator candidates, " +
            "strength, shadow path, and original locator into their capabilities.")]
        public void ChromiumLocatorCandidatesTest()
        {
            // Arrange: a click whose flagged trigger node carries the complete locator contract of the recorder.
            var click = NewEvent(eventName: "InvokeClick", timestamp: 1, value: new JsonObject());
            click["chain"]["path"][0]["isTriggerElement"] = true;
            click["chain"]["path"][0]["properties"] = NewLocatorProperties();
            var properties = click["chain"]["path"][0]["properties"];

            // Act: convert the capture and read the capabilities of the click rule.
            var rule = NewChromiumRules(events: [click])[0];
            var capabilities = rule["capabilities"];

            // Assert: the rule keeps the chain locator and names no locator type, so the engine defaults to XPath.
            Assert.AreEqual(expected: "//target", actual: rule["onElement"].GetValue<string>());
            Assert.IsNull(value: rule["locator"]);

            // Assert: the four locator properties join the element name with their recorded values.
            var locators = capabilities["locators"];
            var shadowPath = capabilities["shadowPath"];
            var originalLocator = capabilities["originalLocator"].GetValue<string>();
            Assert.AreEqual(expected: "target", actual: capabilities["elementName"].GetValue<string>());
            Assert.IsTrue(condition: JsonNode.DeepEquals(node1: properties["locators"], node2: locators));
            Assert.AreEqual(expected: "Strong", actual: capabilities["locatorStrength"].GetValue<string>());
            Assert.IsTrue(condition: JsonNode.DeepEquals(node1: properties["shadowPath"], node2: shadowPath));
            Assert.AreEqual(expected: "/html[1]/body[1]/button[1]", actual: originalLocator);

            // Assert: recorder-only properties stay on the event, and the rule owns detached copies of the arrays.
            Assert.IsNull(value: capabilities["locatorId"]);
            Assert.IsNull(value: capabilities["cssSelector"]);
            Assert.IsNull(value: capabilities["tagName"]);
            Assert.AreNotSame(notExpected: properties["locators"], actual: locators);
        }

        [TestMethod(DisplayName = "Verify Chromium locator capabilities coexist with the DOM context " +
            "on a rule whose element has no name.")]
        public void ChromiumLocatorCandidatesWithDomContextTest()
        {
            // Arrange: an unnamed field carries a DOM context beside the locator contract, so neither the element
            // name nor a previous capability creates the capabilities object for the other.
            var text = NewEvent(eventName: "SendKeys", timestamp: 1, value: new JsonObject { ["text"] = "x" });
            var properties = NewLocatorProperties();
            properties["domContext"] = "PGkvPg==";
            properties["domContextLength"] = 4;
            text["chain"]["path"][0] = new JsonObject { ["name"] = "", ["properties"] = properties };

            // Act: convert the capture and read the capabilities of the typed-text rule.
            var rule = NewChromiumRules(events: [text])[0];
            var capabilities = rule["capabilities"].AsObject();

            // Assert: the DOM context and the locator properties share one capabilities object, with nothing else.
            Assert.AreEqual(expected: "PGkvPg==", actual: capabilities["domContext"].GetValue<string>());
            Assert.AreEqual(expected: 4, actual: capabilities["domContextLength"].GetValue<long>());
            Assert.HasCount(expected: 2, collection: capabilities["locators"].AsArray());
            Assert.AreEqual(expected: "Strong", actual: capabilities["locatorStrength"].GetValue<string>());
            Assert.HasCount(expected: 2, collection: capabilities["shadowPath"].AsArray());
            Assert.IsNotNull(value: capabilities["originalLocator"]);
            Assert.IsNull(value: capabilities["elementName"]);
            Assert.HasCount(expected: 6, collection: capabilities);
        }

        [TestMethod(DisplayName = "Verify UIA coordinate mode emits physical input without element targets.")]
        public void CoordinateMouseTest()
        {
            // Arrange: desktop coordinates and a valid element chain reproduce the UIA release contract.
            var options = new RecorderModel
            {
                BaseUrl = "http://localhost:9955",
                Mode = "coordinate",
                DriverParameters = new Dictionary<string, object> { ["driver"] = "UiaDriver" }
            };
            var mouse = NewEvent(eventName: "Right Up", timestamp: 1, value: new JsonObject { ["x"] = 123, ["y"] = 456 });

            // Act: resolve physical playback from the captured pointer coordinates.
            var automation = AutomationBuilder.NewAutomation(
                authentication: new RecordingAuthenticationModel { Token = "" },
                captures: [new RecorderCapture(Options: options, Events: [mouse])]);
            var rule = automation["stages"][0]["jobs"][0]["rules"][0];

            // Assert: coordinate replay has no locator and carries both absolute axes.
            Assert.AreEqual(expected: "InvokeUser32ContextClick", actual: rule["pluginName"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --X:123 --Y:456}}", actual: rule["argument"].GetValue<string>());
            Assert.IsNull(value: rule["onElement"]);
        }

        [TestMethod(DisplayName = "Verify UIA text, Space, modifiers, discrete keys, and following clicks remain ordered.")]
        public void UiaKeyboardAndOffsetTest()
        {
            // Arrange: key transitions include a standalone modifier and end with a click on another element.
            var options = new RecorderModel
            {
                BaseUrl = "http://localhost:9955",
                Mode = "user32",
                UseOffset = true,
                DriverParameters = new Dictionary<string, object> { ["driver"] = "UiaDriver" }
            };
            var events = new[]
            {
                NewEvent(eventName: "Key Down", timestamp: 1, value: new JsonObject { ["key"] = "h" }),
                NewEvent(eventName: "Key Up", timestamp: 2, value: new JsonObject { ["key"] = "h" }),
                NewEvent(eventName: "Key Up", timestamp: 3, value: new JsonObject { ["key"] = "Space" }),
                NewEvent(eventName: "Key Up", timestamp: 4, value: new JsonObject { ["key"] = "Shift", ["virtualKey"] = 0x10 }),
                NewEvent(eventName: "Key Up", timestamp: 5, value: new JsonObject { ["key"] = "Enter" }),
                NewEvent(eventName: "Left Up", timestamp: 6, value: new JsonObject { ["x"] = 10, ["y"] = 20 })
            };

            // Arrange: attach element-relative offsets only to the terminal click, keeping keyboard capture unchanged.
            events[^1]["chain"]["mouseOffset"] = new JsonObject { ["x"] = 3, ["y"] = 4 };

            // Act: convert low-level releases into ordered playback rules.
            var automation = AutomationBuilder.NewAutomation(
                authentication: new RecordingAuthenticationModel { Token = "" },
                captures: [new RecorderCapture(Options: options, Events: events)]);
            var rules = automation["stages"][0]["jobs"][0]["rules"].AsArray();

            // Assert: text coalesces, modifiers vanish, and the mouse survives keyboard consumption with TopLeft offsets.
            Assert.HasCount(expected: 4, collection: rules);
            Assert.AreEqual(expected: "{{$ --Keys:h }}", actual: rules[0]["argument"].GetValue<string>());
            Assert.AreEqual(expected: "SendUser32KeyboardKey", actual: rules[1]["pluginName"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --Key:Enter}}", actual: rules[1]["argument"].GetValue<string>());
            Assert.AreEqual(expected: "InvokeUser32Click", actual: rules[2]["pluginName"].GetValue<string>());
            Assert.AreEqual(
                expected: "{{$ --OffsetX:3 --OffsetY:4 --Alignment:TopLeft}}",
                actual: rules[2]["argument"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify excluded processes and window titles never reach the automation.")]
        public void ExcludedWindowsTest()
        {
            // Arrange: four clicks, three of them inside the client's own windows by each matching signal.
            var options = new RecorderModel
            {
                BaseUrl = "http://localhost:9955",
                Mode = "user32",
                DriverParameters = new Dictionary<string, object> { ["driver"] = "UiaDriver" }
            };
            var topWindowClick = NewEvent(eventName: "Left Up", timestamp: 1, value: new JsonObject());
            var nodeClick = NewEvent(eventName: "Left Up", timestamp: 2, value: new JsonObject());
            var titleClick = NewEvent(eventName: "Left Up", timestamp: 3, value: new JsonObject());
            var targetClick = NewEvent(eventName: "Left Up", timestamp: 4, value: new JsonObject());
            topWindowClick["chain"]["topWindow"] = new JsonObject { ["processId"] = 100, ["name"] = "Code" };
            nodeClick["chain"]["path"][0]["processId"] = 200;
            var debugHostWindow = new JsonObject { ["processId"] = 7, ["name"] = "[Extension Development Host] x" };
            titleClick["chain"]["topWindow"] = debugHostWindow;
            targetClick["chain"]["topWindow"] = new JsonObject { ["processId"] = 7, ["name"] = "Calculator" };
            var events = new[] { topWindowClick, nodeClick, titleClick, targetClick };

            // Act: convert with the client's process family and its debug-host title fragment excluded.
            var recording = AutomationBuilder.NewRecording(
                authentication: new RecordingAuthenticationModel { Token = "" },
                captures: [new RecorderCapture(Options: options, Events: events)],
                excludedProcessIds: [100, 200],
                excludedWindowTitles: ["extension development host"]);
            var rules = recording.Automation["stages"][0]["jobs"][0]["rules"].AsArray();

            // Assert: only the click in the unrelated window survives, followed by the closing action.
            Assert.HasCount(expected: 2, collection: rules);
            Assert.AreEqual(expected: 4L, actual: rules[0]["context"]["timestamp"].GetValue<long>());
        }

        [TestMethod(DisplayName = "Verify the job map names the recorder of every job in job order.")]
        public void JobMapTest()
        {
            // Arrange: events interleave two endpoints as A -> B -> A, which forms three jobs.
            var first = new RecorderModel
            {
                BaseUrl = "http://localhost:9955",
                DriverParameters = new Dictionary<string, object> { ["driver"] = "UiaDriver" }
            };
            var second = new RecorderModel
            {
                BaseUrl = "http://localhost:9956",
                DriverParameters = new Dictionary<string, object> { ["driver"] = "UiaDriver" }
            };
            var firstEvents = new[]
            {
                NewEvent(eventName: "Left Up", timestamp: 1, value: new JsonObject()),
                NewEvent(eventName: "Left Up", timestamp: 3, value: new JsonObject())
            };
            var secondEvents = new[] { NewEvent(eventName: "Left Up", timestamp: 2, value: new JsonObject()) };

            // Act: convert both captures and read the job map beside the flow.
            var recording = AutomationBuilder.NewRecording(
                authentication: new RecordingAuthenticationModel { Token = "" },
                captures: [new RecorderCapture(first, firstEvents), new RecorderCapture(second, secondEvents)],
                excludedProcessIds: [],
                excludedWindowTitles: []);
            var jobs = recording.Automation["stages"][0]["jobs"].AsArray();

            // Assert: each map entry matches its job's position, reference id, and recorder.
            Assert.HasCount(expected: 3, collection: recording.Jobs);
            Assert.AreEqual(expected: "http://localhost:9955", actual: recording.Jobs[0].BaseUrl);
            Assert.AreEqual(expected: "http://localhost:9956", actual: recording.Jobs[1].BaseUrl);
            Assert.AreEqual(expected: "http://localhost:9955", actual: recording.Jobs[2].BaseUrl);
            Assert.AreEqual(expected: 2, actual: recording.Jobs[2].Index);
            Assert.AreEqual(expected: jobs[1]["reference"]["id"].GetValue<string>(), actual: recording.Jobs[1].Id);
        }

        [TestMethod(DisplayName = "Verify legacy streams without payload modifiers still produce chords.")]
        public void UiaLegacyStreamChordTest()
        {
            // Arrange: an older recorder reports Ctrl+A only as ordered presses and releases.
            var events = new[]
            {
                NewKeyEvent(eventName: "Key Down", timestamp: 1, key: "LCtrl", virtualKey: 0xA2),
                NewKeyEvent(eventName: "Key Down", timestamp: 2, key: "a", virtualKey: 0x41),
                NewKeyEvent(eventName: "Key Up", timestamp: 3, key: "a", virtualKey: 0x41),
                NewKeyEvent(eventName: "Key Up", timestamp: 4, key: "LCtrl", virtualKey: 0xA2),
                NewKeyEvent(eventName: "Key Up", timestamp: 5, key: "b", virtualKey: 0x42)
            };

            // Act: convert the stream without any modifiers field.
            var rules = NewUiaRules(events);

            // Assert: the held Control turns the letter into a chord, and the later letter stays text.
            var chord = rules[0]["argument"].GetValue<string>();
            Assert.HasCount(expected: 2, collection: rules);
            Assert.AreEqual(expected: "{{$ --Key:Ctrl --Key:A --Sticky}}", actual: chord);
            Assert.AreEqual(expected: "{{$ --Keys:b}}", actual: rules[1]["argument"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify a lone Windows key plays as the key itself and ends a typed-text run.")]
        public void UiaLoneWindowsKeyTest()
        {
            // Arrange: text is typed around a lone left Windows press, then the right Windows key is tapped alone.
            var events = new[]
            {
                NewKeyEvent(eventName: "Key Down", timestamp: 1, key: "h", virtualKey: 0x48, modifiers: []),
                NewKeyEvent(eventName: "Key Up", timestamp: 2, key: "h", virtualKey: 0x48, modifiers: []),
                NewKeyEvent(eventName: "Key Down", timestamp: 3, key: "Left Windows", virtualKey: 0x5B, modifiers: []),
                NewKeyEvent(eventName: "Key Down", timestamp: 4, key: "Left Windows", virtualKey: 0x5B, modifiers: []),
                NewKeyEvent(eventName: "Key Up", timestamp: 5, key: "Left Windows", virtualKey: 0x5B, modifiers: []),
                NewKeyEvent(eventName: "Key Down", timestamp: 6, key: "i", virtualKey: 0x49, modifiers: []),
                NewKeyEvent(eventName: "Key Up", timestamp: 7, key: "i", virtualKey: 0x49, modifiers: []),
                NewKeyEvent(eventName: "Key Down", timestamp: 8, key: "Right Windows", virtualKey: 0x5C, modifiers: []),
                NewKeyEvent(eventName: "Key Up", timestamp: 9, key: "Right Windows", virtualKey: 0x5C, modifiers: [])
            };

            // Act: convert the stream, including the auto-repeated left Windows press.
            var rules = NewUiaRules(events);

            // Assert: each lone Windows key is its own User32 action that splits the text into two runs.
            Assert.HasCount(expected: 4, collection: rules);
            Assert.AreEqual(expected: "{{$ --Keys:h}}", actual: rules[0]["argument"].GetValue<string>());
            Assert.AreEqual(expected: "SendUser32KeyboardKey", actual: rules[1]["pluginName"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --Key:LWin --Sticky}}", actual: rules[1]["argument"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --Keys:i}}", actual: rules[2]["argument"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --Key:RWin --Sticky}}", actual: rules[3]["argument"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify recorder-reported modifiers produce sticky chords and keep shifted text.")]
        public void UiaReportedModifiersTest()
        {
            // Arrange: Ctrl+A is reported as a chord, and Shift-typed text carries an empty modifier list.
            var events = new[]
            {
                NewKeyEvent(eventName: "Key Up", timestamp: 1, key: "A", virtualKey: 0x41, modifiers: ["Ctrl"]),
                NewKeyEvent(eventName: "Key Up", timestamp: 2, key: "E", virtualKey: 0x45, modifiers: []),
                NewKeyEvent(eventName: "Key Up", timestamp: 3, key: ":", virtualKey: 0xBA, modifiers: []),
                NewKeyEvent(eventName: "Key Up", timestamp: 4, key: "Tab", virtualKey: 0x09, modifiers: ["Shift"])
            };

            // Act: convert the reported stream.
            var rules = NewUiaRules(events);

            // Assert: chords become sticky User32 keys, and shifted glyphs stay one text run.
            var selectAll = rules[0]["argument"].GetValue<string>();
            var reverseTab = rules[2]["argument"].GetValue<string>();
            Assert.HasCount(expected: 3, collection: rules);
            Assert.AreEqual(expected: "SendUser32KeyboardKey", actual: rules[0]["pluginName"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --Key:Ctrl --Key:A --Sticky}}", actual: selectAll);
            Assert.AreEqual(expected: "{{$ --Keys:E:}}", actual: rules[1]["argument"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --Key:LShift --Key:Tab --Sticky}}", actual: reverseTab);
        }

        [TestMethod(DisplayName = "Verify untargeted keys are kept and untargeted clicks are dropped.")]
        public void UiaUntargetedInputTest()
        {
            // Arrange: a run starts on a targeted key and continues with keys whose snapshot was missing.
            var events = new[]
            {
                NewKeyEvent(eventName: "Key Up", timestamp: 1, key: "p", virtualKey: 0x50, modifiers: []),
                NewKeyEvent(eventName: "Key Up", timestamp: 2, key: "o", virtualKey: 0x4F, modifiers: []),
                NewKeyEvent(eventName: "Key Up", timestamp: 3, key: "e", virtualKey: 0x45, modifiers: []),
                NewEvent(eventName: "Left Up", timestamp: 4, value: new JsonObject { ["x"] = 1, ["y"] = 2 })
            };
            events[1].Remove(propertyName: "chain");
            events[2].Remove(propertyName: "chain");
            events[3].Remove(propertyName: "chain");

            // Act: convert the partially targeted stream.
            var rules = NewUiaRules(events);

            // Assert: every key survives in one run that keeps the targeted locator; the blind click is dropped.
            Assert.HasCount(expected: 1, collection: rules);
            Assert.AreEqual(expected: "{{$ --Keys:poe}}", actual: rules[0]["argument"].GetValue<string>());
            Assert.AreEqual(expected: "//target", actual: rules[0]["onElement"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify a Windows key used in a chord or with a click never plays on its own.")]
        public void UiaWindowsKeyChordTest()
        {
            // Arrange: Win+R is pressed as a chord, then the right Windows key is held during a click.
            var events = new[]
            {
                NewKeyEvent(eventName: "Key Down", timestamp: 1, key: "Left Windows", virtualKey: 0x5B, modifiers: []),
                NewKeyEvent(eventName: "Key Down", timestamp: 2, key: "r", virtualKey: 0x52, modifiers: ["Win"]),
                NewKeyEvent(eventName: "Key Up", timestamp: 3, key: "r", virtualKey: 0x52, modifiers: ["Win"]),
                NewKeyEvent(eventName: "Key Up", timestamp: 4, key: "Left Windows", virtualKey: 0x5B, modifiers: []),
                NewKeyEvent(eventName: "Key Down", timestamp: 5, key: "Right Windows", virtualKey: 0x5C, modifiers: []),
                NewEvent(eventName: "Left Down", timestamp: 6, value: new JsonObject { ["x"] = 1, ["y"] = 2 }),
                NewEvent(eventName: "Left Up", timestamp: 7, value: new JsonObject { ["x"] = 1, ["y"] = 2 }),
                NewKeyEvent(eventName: "Key Up", timestamp: 8, key: "Right Windows", virtualKey: 0x5C, modifiers: [])
            };

            // Act: convert the stream.
            var rules = NewUiaRules(events);

            // Assert: only the chord and the click remain; neither Windows release becomes an action.
            var runDialog = rules[0]["argument"].GetValue<string>();
            Assert.HasCount(expected: 2, collection: rules);
            Assert.AreEqual(expected: "{{$ --Key:Win --Key:R --Sticky}}", actual: runDialog);
            Assert.AreEqual(expected: "InvokeUser32Click", actual: rules[1]["pluginName"].GetValue<string>());
        }

        // Converts one Chromium capture and returns its rules without the trailing CloseBrowser action.
        private static List<JsonNode> NewChromiumRules(JsonObject[] events)
        {
            // Arrange: use a Chromium endpoint without pacing, so every event converts into exactly one rule.
            var options = new RecorderModel
            {
                BaseUrl = "http://localhost:9956",
                DriverParameters = new Dictionary<string, object> { ["driver"] = "ChromeDriver" }
            };

            // Act: convert the capture and drop the endpoint's closing action.
            var automation = AutomationBuilder.NewAutomation(
                authentication: new RecordingAuthenticationModel { Token = "" },
                captures: [new RecorderCapture(Options: options, Events: events)]);
            var rules = automation["stages"][0]["jobs"][0]["rules"].AsArray();
            return [.. rules.Where(predicate: rule => rule["pluginName"].GetValue<string>() != "CloseBrowser")];
        }

        // Creates one trusted G4 model; the surrounding transport envelope is tested separately through HTTP.
        private static JsonObject NewEvent(string eventName, long timestamp, JsonObject value)
        {
            // Arrange: derive the event family from the recorder vocabulary before attaching the shared target chain.
            var isKeyboard = eventName.Contains(value: "Key", comparisonType: StringComparison.OrdinalIgnoreCase);
            return new JsonObject
            {
                ["event"] = eventName,
                ["type"] = isKeyboard ? "Keyboard" : "Mouse",
                ["timestamp"] = timestamp,
                ["machineName"] = "fixture-machine",
                ["value"] = value,
                ["chain"] = new JsonObject
                {
                    ["locator"] = "//target",
                    ["path"] = new JsonArray(new JsonObject { ["name"] = "target" })
                }
            };
        }

        // Creates one keyboard model from a legacy recorder, which reports no modifiers field.
        private static JsonObject NewKeyEvent(string eventName, long timestamp, string key, int virtualKey)
        {
            // Arrange: carry only the key text and virtual-key code, as recorders before chord support did.
            var value = new JsonObject { ["key"] = key, ["virtualKey"] = virtualKey };
            return NewEvent(eventName, timestamp, value);
        }

        // Creates one keyboard model carrying the recorder's press-time shortcut modifiers.
        private static JsonObject NewKeyEvent(
            string eventName,
            long timestamp,
            string key,
            int virtualKey,
            string[] modifiers)
        {
            // Arrange: add the modifiers array that current recorders send with every key event.
            var model = NewKeyEvent(eventName, timestamp, key, virtualKey);
            var names = modifiers.Select(selector: name => (JsonNode)name).ToArray();
            model["value"]["modifiers"] = new JsonArray(names);
            return model;
        }

        // Creates the trigger-node properties the Chromium recorder sends with an element event: two ranked locator
        // candidates, the strength of the chosen locator, the locator it started from, and a shadow-root path.
        private static JsonObject NewLocatorProperties()
        {
            // Arrange: rank a role-and-name XPath above a test-hook CSS selector, both unique on the page.
            var roleName = new JsonObject
            {
                ["using"] = "Xpath",
                ["value"] = "//button[normalize-space()='Save']",
                ["strategy"] = "RoleName",
                ["score"] = 92,
                ["verdict"] = "Unique"
            };
            var testHook = new JsonObject
            {
                ["using"] = "CssSelector",
                ["value"] = "[data-test-id=\"save\"]",
                ["strategy"] = "TestHook",
                ["score"] = 78,
                ["verdict"] = "Unique"
            };

            // Arrange: describe the shadow host and the target inside its shadow root.
            var shadowHost = new JsonObject { ["using"] = "Xpath", ["value"] = "//*[@id='host']" };
            var shadowTarget = new JsonObject { ["using"] = "CssSelector", ["value"] = "button" };

            // Arrange: add the recorder-only members, which conversion keeps out of the rule.
            return new JsonObject
            {
                ["tagName"] = "button",
                ["cssSelector"] = "button.save",
                ["locatorId"] = "5b0c6f0e-0000-4000-8000-000000000001",
                ["locatorStrength"] = "Strong",
                ["locators"] = new JsonArray(roleName, testHook),
                ["originalLocator"] = "/html[1]/body[1]/button[1]",
                ["shadowPath"] = new JsonArray(shadowHost, shadowTarget)
            };
        }

        // Converts one User32 capture and returns its rules without the trailing CloseBrowser action.
        private static List<JsonNode> NewUiaRules(JsonObject[] events)
        {
            // Arrange: use a User32 endpoint, the mode the shortcut and untargeted-key rules apply to.
            var options = new RecorderModel
            {
                BaseUrl = "http://localhost:9955",
                Mode = "user32",
                DriverParameters = new Dictionary<string, object> { ["driver"] = "UiaDriver" }
            };

            // Act: convert the capture and drop the endpoint's closing action.
            var automation = AutomationBuilder.NewAutomation(
                authentication: new RecordingAuthenticationModel { Token = "" },
                captures: [new RecorderCapture(Options: options, Events: events)]);
            var rules = automation["stages"][0]["jobs"][0]["rules"].AsArray();
            return [.. rules.Where(predicate: rule => rule["pluginName"].GetValue<string>() != "CloseBrowser")];
        }
    }
}
