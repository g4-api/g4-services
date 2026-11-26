using G4.Abstraction.Cli;
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
        // Provides the chain locator the recorder sends for the drop element of a drag and drop.
        private const string DropLocator = "//ul[@aria-label='Done']";

        // Provides the chain locator the recorder sends for a target inside a shadow root: the XPath of the
        // outermost shadow host, which is all an XPath can reach.
        private const string ShadowHostLocator = "//*[@id='lab-shadow-host']";

        // Provides the shadow-path candidate value that reaches the real target, one CSS segment per shadow root.
        private const string ShadowPathLocator = "#lab-shadow-host >>> button[name='save']";

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
            Assert.AreEqual(expected: "{{$ --Left:0 --Top:360 --Relative}}", actual: rules[2]["argument"].GetValue<string>());
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
            Assert.AreEqual(expected: "{{$ --Left:0 --Top:360 --Relative}}", actual: rules[2]["argument"].GetValue<string>());
            Assert.AreEqual(expected: "//target", actual: rules[2]["onElement"].GetValue<string>());
            Assert.AreEqual(expected: "//target", actual: rules[3]["onElement"].GetValue<string>());
            Assert.IsNull(value: rules[4]["onElement"]);
            Assert.IsNull(value: rules[5]["onElement"]);

            // Assert: the long press keeps its hold time and the activity change names the screen to start.
            Assert.AreEqual(expected: "{{$ --Duration:500}}", actual: rules[3]["argument"].GetValue<string>());
            Assert.AreEqual(
                expected: "{{$ --Package:com.app --Activity:com.app.Home}}",
                actual: rules[4]["argument"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify an Android long press without a usable hold time leaves the duration to " +
            "the plugin default.")]
        #region *** Data Set ***
        [DataRow(0d)]
        [DataRow(-20d)]
        #endregion
        public void AndroidLongClickDefaultDurationTest(double durationMilliseconds)
        {
            // Arrange: a long press whose recorded hold time is zero or negative, plus one without a value at all.
            var value = new JsonObject { ["durationMilliseconds"] = durationMilliseconds };
            var withDuration = NewEvent(eventName: "InvokeLongClick", timestamp: 1, value);
            var withoutDuration = NewEvent(eventName: "InvokeLongClick", timestamp: 2, value: new JsonObject());

            // Act: convert both events.
            var rules = NewChromiumRules(events: [withDuration, withoutDuration]);

            // Assert: neither rule sets --Duration, so the plugin holds for its 1000 millisecond default.
            Assert.AreEqual(expected: string.Empty, actual: rules[0]["argument"].GetValue<string>());
            Assert.AreEqual(expected: string.Empty, actual: rules[1]["argument"].GetValue<string>());
            Assert.AreEqual(expected: "//target", actual: rules[0]["onElement"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify an Android activity change without a package passes the activity alone.")]
        public void AndroidActivityWithoutPackageTest()
        {
            // Arrange: an activity change whose value names a full intent and no package.
            var value = new JsonObject { ["activity"] = "com.app/.Home" };
            var activity = NewEvent(eventName: "SwitchActivity", timestamp: 1, value);

            // Act: convert the event.
            var rule = NewChromiumRules(events: [activity])[0];

            // Assert: only --Activity is written, so the plugin uses the full intent as it is, and no element is set.
            Assert.AreEqual(expected: "{{$ --Activity:com.app/.Home}}", actual: rule["argument"].GetValue<string>());
            Assert.IsNull(value: rule["onElement"]);
        }

        [TestMethod(DisplayName = "Verify a Chromium middle click becomes an InvokeMiddleClick rule on the " +
            "recorded element.")]
        public void ChromiumMiddleClickTest()
        {
            // Arrange: a middle click as the Chromium recorder sends it for an auxclick with button 1.
            var middleClick = NewEvent(eventName: "InvokeMiddleClick", timestamp: 1, value: new JsonObject());

            // Act: convert the event.
            var rule = NewChromiumRules(events: [middleClick])[0];

            // Assert: the plugin name passes through and the rule targets the recorded element without an argument.
            Assert.AreEqual(expected: "InvokeMiddleClick", actual: rule["pluginName"].GetValue<string>());
            Assert.AreEqual(expected: "//target", actual: rule["onElement"].GetValue<string>());
            Assert.IsNull(value: rule["argument"]);
        }

        [TestMethod(DisplayName = "Verify a Chromium wheel event scrolls by its recorded pixel deltas, rounded and " +
            "never multiplied by its notch count.")]
        #region *** Data Set ***
        [DataRow(0, 250.4, "{{$ --Left:0 --Top:250 --Relative}}")]
        [DataRow(-96, 0, "{{$ --Left:-96 --Top:0 --Relative}}")]
        [DataRow(40, -360, "{{$ --Left:40 --Top:-360 --Relative}}")]
        #endregion
        public void ChromiumScrollDeltaTest(double deltaX, double deltaY, string expected)
        {
            // Arrange: a wheel event as the Chromium recorder sends it, with a direction and notches that disagree
            // with the deltas, so only the deltas can produce the expected offsets.
            var value = new JsonObject
            {
                ["direction"] = "Down",
                ["notches"] = 3,
                ["deltaX"] = deltaX,
                ["deltaY"] = deltaY
            };
            var scroll = NewEvent(eventName: "InvokeScroll", timestamp: 1, value);

            // Act: convert the scroll event.
            var rule = NewChromiumRules(events: [scroll])[0];

            // Assert: the rule scrolls relative by the deltas and keeps the recorded element.
            Assert.AreEqual(expected: "InvokeScroll", actual: rule["pluginName"].GetValue<string>());
            Assert.AreEqual(expected, actual: rule["argument"].GetValue<string>());
            Assert.AreEqual(expected: "//target", actual: rule["onElement"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify a scroll with a single numeric delta scrolls by that delta and ignores " +
            "a delta that is not a number.")]
        public void ChromiumScrollPartialDeltaTest()
        {
            // Arrange: only the vertical delta is a number; the horizontal one is text.
            var value = new JsonObject { ["direction"] = "Left", ["notches"] = 2, ["deltaX"] = "12", ["deltaY"] = 80 };
            var scroll = NewEvent(eventName: "InvokeScroll", timestamp: 1, value);

            // Act: convert the scroll event.
            var rule = NewChromiumRules(events: [scroll])[0];

            // Assert: the numeric delta wins over the notches, and the text delta counts as zero.
            Assert.AreEqual(expected: "{{$ --Left:0 --Top:80 --Relative}}", actual: rule["argument"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify a scroll without deltas, as the Android recorder sends it, scrolls 120 " +
            "pixels per notch along its direction.")]
        #region *** Data Set ***
        [DataRow("Down", "{{$ --Left:0 --Top:240 --Relative}}")]
        [DataRow("Up", "{{$ --Left:0 --Top:-240 --Relative}}")]
        [DataRow("Right", "{{$ --Left:240 --Top:0 --Relative}}")]
        [DataRow("Left", "{{$ --Left:-240 --Top:0 --Relative}}")]
        #endregion
        public void ScrollDirectionTest(string direction, string expected)
        {
            // Arrange: an Android scroll gesture of two notches, which carries no pixel deltas.
            var options = new RecorderModel
            {
                BaseUrl = "http://localhost:9958",
                DriverParameters = new Dictionary<string, object> { ["driver"] = "AndroidDriver" }
            };
            var value = new JsonObject { ["direction"] = direction, ["notches"] = 2 };
            var scroll = NewEvent(eventName: "InvokeScroll", timestamp: 1, value);

            // Act: convert the capture into a flow.
            var automation = AutomationBuilder.NewAutomation(
                authentication: new RecordingAuthenticationModel { Token = "" },
                captures: [new RecorderCapture(Options: options, Events: [scroll])]);
            var rule = automation["stages"][0]["jobs"][0]["rules"][0];

            // Assert: the notches become a signed pixel offset on the direction's axis only.
            Assert.AreEqual(expected, actual: rule["argument"].GetValue<string>());
            Assert.AreEqual(expected: "//target", actual: rule["onElement"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify a scroll without a direction scrolls down and one without a usable notch " +
            "count scrolls one notch.")]
        #region *** Data Set ***
        [DataRow(null, null, "{{$ --Left:0 --Top:120 --Relative}}")]
        [DataRow(null, "3", "{{$ --Left:0 --Top:360 --Relative}}")]
        [DataRow("\"Up\"", null, "{{$ --Left:0 --Top:-120 --Relative}}")]
        [DataRow("\"\"", "0", "{{$ --Left:0 --Top:120 --Relative}}")]
        [DataRow("\"Right\"", "\"many\"", "{{$ --Left:120 --Top:0 --Relative}}")]
        #endregion
        public void ScrollDefaultsTest(string directionJson, string notchesJson, string expected)
        {
            // Arrange: write only the fields the row names, so an absent field stays absent.
            var value = new JsonObject();
            if (directionJson is not null)
            {
                value["direction"] = JsonNode.Parse(json: directionJson);
            }

            if (notchesJson is not null)
            {
                value["notches"] = JsonNode.Parse(json: notchesJson);
            }

            var scroll = NewEvent(eventName: "InvokeScroll", timestamp: 1, value);

            // Act: convert the scroll event.
            var rule = NewChromiumRules(events: [scroll])[0];

            // Assert: the defaults fill in what the recorder left out.
            Assert.AreEqual(expected, actual: rule["argument"].GetValue<string>());
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

        [TestMethod(DisplayName = "Verify a Chromium rule without a shadow-path candidate keeps the chain locator " +
            "and names no locator type.")]
        public void ChromiumShadowLocatorAbsentTest()
        {
            // Arrange: a click whose trigger node ranks only ordinary candidates, although it also carries the
            // shadow-root steps, so nothing but a ShadowPath candidate can retarget the rule.
            var click = NewEvent(eventName: "InvokeClick", timestamp: 1, value: new JsonObject());
            click["chain"]["path"][0]["properties"] = NewLocatorProperties();

            // Act: convert the capture and read the member names of the click rule in serialization order.
            var rule = NewChromiumRules(events: [click])[0].AsObject();
            var names = rule.Select(selector: property => property.Key).ToArray();

            // Assert: the rule keeps the chain locator and the member set it had before shadow paths existed.
            string[] expectedNames = ["$type", "pluginName", "context", "onElement", "capabilities"];
            Assert.AreEqual(expected: "//target", actual: rule["onElement"].GetValue<string>());
            Assert.IsFalse(condition: rule.ContainsKey(propertyName: "locator"));
            Assert.AreSequenceEqual(expected: expectedNames, actual: names);
        }

        [TestMethod(DisplayName = "Verify a Broken shadow-path candidate is ignored, so the rule falls back to " +
            "the host locator of the chain.")]
        public void ChromiumShadowLocatorBrokenTest()
        {
            // Arrange: the recorder re-tested the shadow path against the live page and reported it broken.
            var broken = NewShadowLocator(value: ShadowPathLocator, verdict: "Broken");
            var click = NewShadowEvent(eventName: "InvokeClick", value: new JsonObject(), locators: [broken]);

            // Act: convert the capture and read the click rule.
            var rule = NewChromiumRules(events: [click])[0].AsObject();
            var locators = rule["capabilities"]["locators"].AsArray();

            // Assert: the rule replays against the shadow host as an XPath, with no locator type.
            Assert.AreEqual(expected: ShadowHostLocator, actual: rule["onElement"].GetValue<string>());
            Assert.IsFalse(condition: rule.ContainsKey(propertyName: "locator"));

            // Assert: the broken candidate still travels with the rule, so a client can inspect or heal it.
            Assert.HasCount(expected: 1, collection: locators);
            Assert.AreEqual(expected: "Broken", actual: locators[0]["verdict"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify the shadow path applies to every Chromium event family that targets " +
            "an element and never to a page-level rule.")]
        public void ChromiumShadowLocatorEventFamiliesTest()
        {
            // Arrange: typed text, a click, and a scroll inside a shadow root, each with its own shadow-path
            // candidate because a JSON node has a single parent.
            var textValue = new JsonObject { ["text"] = "hello" };
            var scrollValue = new JsonObject { ["direction"] = "Up", ["notches"] = 2 };
            var text = NewShadowEvent(
                eventName: "SendKeys",
                value: textValue,
                locators: [NewShadowLocator(value: ShadowPathLocator, verdict: "Unique")]);
            var click = NewShadowEvent(
                eventName: "InvokeClick",
                value: new JsonObject(),
                locators: [NewShadowLocator(value: ShadowPathLocator, verdict: "Unique")]);
            var scroll = NewShadowEvent(
                eventName: "InvokeScroll",
                value: scrollValue,
                locators: [NewShadowLocator(value: ShadowPathLocator, verdict: "Unique")]);

            // Arrange: a navigation carries the same candidate on a rule that targets no element.
            var navigation = NewShadowEvent(
                eventName: "OpenUrl",
                value: new JsonObject { ["url"] = "https://example.com" },
                locators: [NewShadowLocator(value: ShadowPathLocator, verdict: "Unique")]);

            // Act: convert the four events in one capture; equal timestamps keep their recorded order.
            var rules = NewChromiumRules(events: [text, click, scroll, navigation]);

            // Assert: the typed-text rule targets the shadow path and still carries the keys macro.
            Assert.AreEqual(expected: ShadowPathLocator, actual: rules[0]["onElement"].GetValue<string>());
            Assert.AreEqual(expected: "CssSelector", actual: rules[0]["locator"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --keys:hello}}", actual: rules[0]["argument"].GetValue<string>());

            // Assert: the click rule and the scroll rule target the shadow path the same way.
            var scrollArgument = rules[2]["argument"].GetValue<string>();
            Assert.AreEqual(expected: ShadowPathLocator, actual: rules[1]["onElement"].GetValue<string>());
            Assert.AreEqual(expected: "CssSelector", actual: rules[1]["locator"].GetValue<string>());
            Assert.AreEqual(expected: ShadowPathLocator, actual: rules[2]["onElement"].GetValue<string>());
            Assert.AreEqual(expected: "CssSelector", actual: rules[2]["locator"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --Left:0 --Top:-240 --Relative}}", actual: scrollArgument);

            // Assert: the navigation rule gains neither a target nor a locator type.
            Assert.IsNull(value: rules[3]["onElement"]);
            Assert.IsNull(value: rules[3]["locator"]);
            Assert.AreEqual(expected: "https://example.com", actual: rules[3]["argument"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify the first usable shadow-path candidate in list order becomes the rule " +
            "locator.")]
        public void ChromiumShadowLocatorFirstEntryTest()
        {
            // Arrange: a broken shadow path leads the list, followed by two usable ones with different values.
            var broken = NewShadowLocator(value: "#stale-host >>> button", verdict: "Broken");
            var first = NewShadowLocator(value: ShadowPathLocator, verdict: "Unique");
            var second = NewShadowLocator(value: "#lab-shadow-host >>> button", verdict: "Ambiguous");
            var click = NewShadowEvent(
                eventName: "InvokeClick",
                value: new JsonObject(),
                locators: [broken, first, second]);

            // Act: convert the capture and read the click rule.
            var rule = NewChromiumRules(events: [click])[0];

            // Assert: the broken candidate is skipped and the earlier usable candidate wins over the later one.
            Assert.AreEqual(expected: ShadowPathLocator, actual: rule["onElement"].GetValue<string>());
            Assert.AreEqual(expected: "CssSelector", actual: rule["locator"].GetValue<string>());
        }

        #region *** Data Set ***
        [DataRow("ShadowPath", "Xpath", "//*[@id='lab-shadow-host']//button")]
        [DataRow("ShadowPath", "cssselector", "#lab-shadow-host >>> button[name='save']")]
        [DataRow("ShadowPath", "CssSelector", "")]
        [DataRow("shadowpath", "CssSelector", "#lab-shadow-host >>> button[name='save']")]
        [DataRow("TestHook", "CssSelector", "#lab-shadow-host >>> button[name='save']")]
        #endregion
        [TestMethod(DisplayName = "Verify a candidate is ignored unless its strategy is ShadowPath, its locator " +
            "type is CssSelector, and its value is not empty.")]
        public void ChromiumShadowLocatorIgnoredTest(string strategy, string locatorType, string value)
        {
            // Arrange: one unbroken candidate that misses exactly one part of the selection rule.
            var candidate = new JsonObject
            {
                ["using"] = locatorType,
                ["value"] = value,
                ["strategy"] = strategy,
                ["score"] = 68,
                ["verdict"] = "Unique"
            };
            var click = NewShadowEvent(eventName: "InvokeClick", value: new JsonObject(), locators: [candidate]);

            // Act: convert the capture and read the click rule.
            var rule = NewChromiumRules(events: [click])[0].AsObject();

            // Assert: the rule falls back to the host locator of the chain and names no locator type.
            Assert.AreEqual(expected: ShadowHostLocator, actual: rule["onElement"].GetValue<string>());
            Assert.IsFalse(condition: rule.ContainsKey(propertyName: "locator"));
        }

        [TestMethod(DisplayName = "Verify malformed locator candidates are skipped without failing the " +
            "conversion.")]
        public void ChromiumShadowLocatorMalformedTest()
        {
            // Arrange: entries that are not objects, an empty object, and shadow-path candidates whose members
            // are absent or carry another JSON type.
            var missingValue = new JsonObject { ["using"] = "CssSelector", ["strategy"] = "ShadowPath" };
            var numberValue = NewShadowLocator(value: ShadowPathLocator, verdict: "Unique");
            numberValue["value"] = 5;
            var numberStrategy = NewShadowLocator(value: ShadowPathLocator, verdict: "Unique");
            numberStrategy["strategy"] = 1;
            var arrayLocatorType = NewShadowLocator(value: ShadowPathLocator, verdict: "Unique");
            arrayLocatorType["using"] = new JsonArray();
            JsonNode[] locators =
            [
                ShadowPathLocator,
                5,
                null,
                new JsonArray(),
                new JsonObject(),
                missingValue,
                numberValue,
                numberStrategy,
                arrayLocatorType
            ];
            var click = NewShadowEvent(eventName: "InvokeClick", value: new JsonObject(), locators);

            // Act: convert the capture and read the click rule.
            var rule = NewChromiumRules(events: [click])[0].AsObject();

            // Assert: no entry qualifies, so the rule keeps the host locator and names no locator type.
            Assert.AreEqual(expected: ShadowHostLocator, actual: rule["onElement"].GetValue<string>());
            Assert.IsFalse(condition: rule.ContainsKey(propertyName: "locator"));

            // Assert: the candidate list is still copied as recorded, malformed entries included.
            Assert.HasCount(expected: 9, collection: rule["capabilities"]["locators"].AsArray());
        }

        [TestMethod(DisplayName = "Verify a Chromium rule whose target is inside a shadow root uses the shadow " +
            "path as a CssSelector locator and keeps its locator capabilities.")]
        public void ChromiumShadowLocatorTest()
        {
            // Arrange: a click inside a shadow root as the recorder sends it: the chain locator reaches only the
            // host, and the flagged trigger node ranks one shadow-path candidate after its two ordinary ones.
            var click = NewEvent(eventName: "InvokeClick", timestamp: 1, value: new JsonObject());
            var properties = NewLocatorProperties();
            var shadowLocator = NewShadowLocator(value: ShadowPathLocator, verdict: "Unique");
            properties["locators"].AsArray().Add(item: shadowLocator);
            click["chain"]["locator"] = ShadowHostLocator;
            click["chain"]["path"][0]["isTriggerElement"] = true;
            click["chain"]["path"][0]["properties"] = properties;

            // Act: convert the capture and read the capabilities of the click rule.
            var rule = NewChromiumRules(events: [click])[0];
            var capabilities = rule["capabilities"];

            // Assert: the rule addresses the real target through the shadow path and names its locator type.
            Assert.AreEqual(expected: ShadowPathLocator, actual: rule["onElement"].GetValue<string>());
            Assert.AreEqual(expected: "CssSelector", actual: rule["locator"].GetValue<string>());

            // Assert: the capabilities are copied as before, with the shadow-path candidate in the full list.
            var locators = capabilities["locators"];
            var shadowPath = capabilities["shadowPath"];
            Assert.AreEqual(expected: "target", actual: capabilities["elementName"].GetValue<string>());
            Assert.HasCount(expected: 3, collection: locators.AsArray());
            Assert.IsTrue(condition: JsonNode.DeepEquals(node1: properties["locators"], node2: locators));
            Assert.IsTrue(condition: JsonNode.DeepEquals(node1: properties["shadowPath"], node2: shadowPath));
            Assert.AreEqual(expected: "Strong", actual: capabilities["locatorStrength"].GetValue<string>());

            // Assert: the buffered event keeps the host locator, so conversion never rewrites recorder data.
            Assert.AreEqual(expected: ShadowHostLocator, actual: click["chain"]["locator"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify a Chromium drag and drop drops on its target XPath and carries the " +
            "target candidates in its capabilities.")]
        public void ChromiumDragTargetTest()
        {
            // Arrange: a drag whose drop element has an XPath chain locator and one ranked candidate.
            var candidate = NewLocator(@using: "Xpath", value: DropLocator);
            var drag = NewDragEvent(targetLocator: DropLocator, targetLocators: [candidate]);

            // Act: convert the capture and read the drag rule.
            var rule = NewChromiumRules(events: [drag])[0];
            var target = rule["capabilities"]["target"];

            // Assert: the source is the chain target, and the argument names the drop XPath without a locator type.
            Assert.AreEqual(expected: "InvokeDragAndDrop", actual: rule["pluginName"].GetValue<string>());
            Assert.AreEqual(expected: "//target", actual: rule["onElement"].GetValue<string>());
            var expectedArgument = "{{$ --Target:" + DropLocator + "}}";
            Assert.AreEqual(expected: expectedArgument, actual: rule["argument"].GetValue<string>());

            // Assert: the drop element is named, and its candidates and strength are copied beside the source ones.
            Assert.AreEqual(expected: "Done", actual: target["elementName"].GetValue<string>());
            Assert.AreEqual(expected: "Strong", actual: target["locatorStrength"].GetValue<string>());
            Assert.HasCount(expected: 1, collection: target["locators"].AsArray());
        }

        [TestMethod(DisplayName = "Verify a Chromium drag and drop onto an element inside a shadow root drops on its " +
            "shadow path as a CssSelector target.")]
        public void ChromiumDragShadowTargetTest()
        {
            // Arrange: the drop element's chain locator reaches only its shadow host, and a shadow path reaches it.
            var drag = NewDragEvent(
                targetLocator: ShadowHostLocator,
                targetLocators: [NewShadowLocator(value: ShadowPathLocator, verdict: "Unique")]);

            // Act: convert the capture and read the drag rule.
            var rule = NewChromiumRules(events: [drag])[0];

            // Assert: the shadow path wins over the host XPath and names its locator type.
            var expected = "{{$ --Target:" + ShadowPathLocator + " --TargetLocator:CssSelector}}";
            Assert.AreEqual(expected: expected, actual: rule["argument"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify a Chromium drag and drop skips target locators the argument cannot carry " +
            "and falls back to the next candidate, then to the recorded distance.")]
        #region *** Data Set ***
        [DataRow("//li[.='a --b']", "#done", "{{$ --Target:#done --TargetLocator:CssSelector}}")]
        [DataRow("//li[.='a --b']", "//li[.='{{$x}}']", "{{$ --DeltaX:341 --DeltaY:-40}}")]
        [DataRow("", "", "{{$ --DeltaX:341 --DeltaY:-40}}")]
        #endregion
        public void ChromiumDragFallbackTest(string targetLocator, string candidateValue, string expected)
        {
            // Arrange: a drag whose chain locator, and maybe its only candidate, would break the argument.
            var candidateType = candidateValue.StartsWith(value: '#') ? "CssSelector" : "Xpath";
            JsonNode[] candidates = candidateValue.Length > 0
                ? [NewLocator(@using: candidateType, value: candidateValue)]
                : [];
            var drag = NewDragEvent(targetLocator, targetLocators: candidates);

            // Act: convert the capture and read the drag argument.
            var argument = NewChromiumRules(events: [drag])[0]["argument"].GetValue<string>();

            // Assert: the first safe candidate wins; with none, the distance is rounded to whole pixels.
            Assert.AreEqual(expected: expected, actual: argument);
        }

        [TestMethod(DisplayName = "Verify the engine's CLI parser reads a drag and drop argument back as the " +
            "recorded target, including quotes, spaces, colons, and a shadow path.")]
        #region *** Data Set ***
        [DataRow("//button[normalize-space()='Move: card 1 - draft']", "Xpath")]
        [DataRow("lab-board >>> ul[aria-label=\"Done\"]", "CssSelector")]
        #endregion
        public void ChromiumDragArgumentParseTest(string targetLocator, string locatorType)
        {
            // Arrange: a drag whose drop element is reached by the given locator alone.
            var candidate = locatorType == "Xpath"
                ? NewLocator(@using: "Xpath", value: targetLocator)
                : NewShadowLocator(value: targetLocator, verdict: "Unique");
            var chainLocator = locatorType == "Xpath" ? targetLocator : "";
            var drag = NewDragEvent(targetLocator: chainLocator, targetLocators: [candidate]);

            // Act: convert the capture and parse its argument the way the plugin receives it.
            var argument = NewChromiumRules(events: [drag])[0]["argument"].GetValue<string>();
            var parameters = new CliFactory().ConvertToDictionary(cli: argument);

            // Assert: the target arrives whole, and only a non-default locator type is named.
            Assert.AreEqual(expected: targetLocator, actual: parameters["Target"]);
            Assert.AreEqual(expected: locatorType != "Xpath", actual: parameters.ContainsKey(key: "TargetLocator"));
        }

        [TestMethod(DisplayName = "Verify a Chromium drag and drop without a drop element replays the recorded " +
            "distance and carries no target capabilities.")]
        public void ChromiumDragOffsetTest()
        {
            // Arrange: a slider drag, where the recorder sends only the distance from the source center.
            var value = new JsonObject { ["kind"] = "Pointer", ["deltaX"] = 120, ["deltaY"] = 0 };
            var drag = NewEvent(eventName: "InvokeDragAndDrop", timestamp: 1, value);

            // Act: convert the capture and read the drag rule.
            var rule = NewChromiumRules(events: [drag])[0];

            // Assert: the distance becomes the offset, and no target capabilities are invented.
            Assert.AreEqual(expected: "{{$ --DeltaX:120 --DeltaY:0}}", actual: rule["argument"].GetValue<string>());
            Assert.IsNull(value: rule["capabilities"]["target"]);
        }

        [TestMethod(DisplayName = "Verify a UIA drag and drop onto a target maps to InvokeUser32DragAndDrop in " +
            "standard and user32 modes, with the grab and drop offsets from the element corners.")]
        #region *** Data Set ***
        [DataRow("standard")]
        [DataRow("user32")]
        #endregion
        public void UiaDragTargetTest(string mode)
        {
            // Arrange: a release past the drag threshold, grabbed and dropped at measured points of both elements.
            var targetLocator = "//List[@AutomationId='DoneList']";
            var target = new JsonObject { ["locator"] = targetLocator, ["path"] = new JsonArray() };
            var drag = NewUiaDrag(target);
            drag["value"]["drag"]["sourceOffsetX"] = 12;
            drag["value"]["drag"]["sourceOffsetY"] = 3;
            drag["value"]["drag"]["targetOffsetX"] = 0;
            drag["value"]["drag"]["targetOffsetY"] = 8;

            // Act: convert the capture in the given mode.
            var rule = NewUiaRules(events: [drag], mode)[0];

            // Assert: both points are kept from the element corners with the alignment the driver applies offsets
            // to, and a zero offset is still written, because it means the corner itself.
            var expected = "{{$ --Alignment:TopLeft --OffsetX:12 --OffsetY:3 " +
                "--Target://List[@AutomationId='DoneList'] " +
                "--TargetAlignment:TopLeft --TargetOffsetX:0 --TargetOffsetY:8}}";
            Assert.AreEqual(expected: "InvokeUser32DragAndDrop", actual: rule["pluginName"].GetValue<string>());
            Assert.AreEqual(expected: "//target", actual: rule["onElement"].GetValue<string>());
            Assert.AreEqual(expected: expected, actual: rule["argument"].GetValue<string>());
            Assert.AreEqual(expected: "target", actual: rule["capabilities"]["elementName"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify a UIA drag offset that is absent, null, or not a number keeps the " +
            "plugin's center alignment.")]
        #region *** Data Set ***
        [DataRow(null)]
        [DataRow("\"12\"")]
        [DataRow("null")]
        #endregion
        public void UiaDragUnmeasuredOffsetTest(string offsetJson)
        {
            // Arrange: a drag onto a target whose grab offset was not measured, as for an element without bounds.
            var target = new JsonObject { ["locator"] = "//List[@Name='Done']", ["path"] = new JsonArray() };
            var drag = NewUiaDrag(target);
            if (offsetJson is not null)
            {
                drag["value"]["drag"]["sourceOffsetX"] = JsonNode.Parse(json: offsetJson);
                drag["value"]["drag"]["sourceOffsetY"] = 3;
            }

            // Act: convert the capture in user32 mode.
            var argument = NewUiaRules(events: [drag], mode: "user32")[0]["argument"].GetValue<string>();

            // Assert: no alignment or offset is written for either end, so both use the element centers.
            Assert.AreEqual(expected: "{{$ --Target://List[@Name='Done']}}", actual: argument);
        }

        [TestMethod(DisplayName = "Verify a UIA drag and drop from an element without a target replays the distance " +
            "from the grab point.")]
        public void UiaDragOffsetTest()
        {
            // Arrange: a drag with no drop element, pressed at (300, 200) and released at (450, 180).
            var drag = NewUiaDrag(target: null);

            // Act: convert the drag in user32 mode.
            var rule = NewUiaRules(events: [drag], mode: "user32")[0];

            // Assert: the rule starts on the element and replays the distance, so the drop follows the element.
            Assert.AreEqual(expected: "InvokeUser32DragAndDrop", actual: rule["pluginName"].GetValue<string>());
            Assert.AreEqual(expected: "//target", actual: rule["onElement"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --DeltaX:150 --DeltaY:-20}}", actual: rule["argument"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify a UIA drag and drop in coordinate mode replays between the press and the " +
            "release points, without a locator.")]
        public void UiaDragCoordinateTest()
        {
            // Arrange: a drag onto a target, recorded in coordinate mode, which replays screen positions only.
            var target = new JsonObject { ["locator"] = "//List[@Name='Done']", ["path"] = new JsonArray() };
            var drag = NewUiaDrag(target);

            // Act: convert the drag in coordinate mode.
            var rule = NewUiaRules(events: [drag], mode: "coordinate")[0];

            // Assert: both ends are screen points, and the rule names no element.
            var expected = "{{$ --X:300 --Y:200 --TargetX:450 --TargetY:180}}";
            Assert.AreEqual(expected: "InvokeUser32DragAndDrop", actual: rule["pluginName"].GetValue<string>());
            Assert.AreEqual(expected: expected, actual: rule["argument"].GetValue<string>());
            Assert.IsNull(value: rule["onElement"]);
        }

        [TestMethod(DisplayName = "Verify a UIA drag and drop pressed over no resolved element starts at the press " +
            "point, and ends on the target element or at the release point.")]
        #region *** Data Set ***
        [DataRow(true, "{{$ --X:300 --Y:200 --Target://List[@Name='Done']}}")]
        [DataRow(false, "{{$ --X:300 --Y:200 --TargetX:450 --TargetY:180}}")]
        #endregion
        public void UiaDragWithoutSourceTest(bool isTarget, string expected)
        {
            // Arrange: a drag whose release carries no chain, because UI Automation resolved nothing at the press.
            var target = isTarget
                ? new JsonObject { ["locator"] = "//List[@Name='Done']", ["path"] = new JsonArray() }
                : null;
            var drag = NewUiaDrag(target);
            drag.Remove(propertyName: "chain");

            // Act: convert the drag in user32 mode.
            var rules = NewUiaRules(events: [drag], mode: "user32");

            // Assert: the drag is kept and starts at the screen point, with no element to name.
            Assert.HasCount(expected: 1, collection: rules);
            Assert.AreEqual(expected: expected, actual: rules[0]["argument"].GetValue<string>());
            Assert.IsNull(value: rules[0]["onElement"]);
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

        [TestMethod(DisplayName = "Verify a UIA middle-button release maps to InvokeMiddleClick in standard mode " +
            "and to InvokeUser32MiddleClick otherwise.")]
        #region *** Data Set ***
        [DataRow("standard", "InvokeMiddleClick", "//target", null)]
        [DataRow("user32", "InvokeUser32MiddleClick", "//target", null)]
        [DataRow("coordinate", "InvokeUser32MiddleClick", null, "{{$ --X:123 --Y:456}}")]
        #endregion
        public void UiaMiddleClickTest(string mode, string expectedPlugin, string expectedLocator, string expectedArgument)
        {
            // Arrange: the middle-button press and release the UIA recorder sends; only the release plays.
            var events = new[]
            {
                NewEvent(eventName: "Middle Down", timestamp: 1, value: new JsonObject { ["x"] = 123, ["y"] = 456 }),
                NewEvent(eventName: "Middle Up", timestamp: 2, value: new JsonObject { ["x"] = 123, ["y"] = 456 })
            };

            // Act: convert the stream in the given capture mode.
            var rules = NewUiaRules(events, mode);

            // Assert: one middle click, on the element or at the screen point the mode records.
            Assert.HasCount(expected: 1, collection: rules);
            Assert.AreEqual(expected: expectedPlugin, actual: rules[0]["pluginName"].GetValue<string>());
            Assert.AreEqual(expected: expectedLocator, actual: rules[0]["onElement"]?.GetValue<string>());
            Assert.AreEqual(expected: expectedArgument, actual: rules[0]["argument"]?.GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify a discrete key keeps its element only for SendKeyboardKey, while " +
            "SendUser32KeyboardKey sends to the focused window without onElement.")]
        #region *** Data Set ***
        [DataRow("standard", "SendKeyboardKey", "//target")]
        [DataRow("user32", "SendUser32KeyboardKey", null)]
        [DataRow("coordinate", "SendUser32KeyboardKey", null)]
        #endregion
        public void UiaKeyboardKeyTargetTest(string mode, string expectedPlugin, string expectedLocator)
        {
            // Arrange: one discrete key release on a recorded element.
            var events = new[] { NewKeyEvent(eventName: "Key Up", timestamp: 1, key: "Enter", virtualKey: 0x0D) };

            // Act: convert the stream in the given capture mode.
            var rules = NewUiaRules(events, mode);

            // Assert: only the element-pattern plugin names the element, and only it carries the element name.
            var isTargeted = expectedLocator is not null;
            Assert.HasCount(expected: 1, collection: rules);
            Assert.AreEqual(expected: expectedPlugin, actual: rules[0]["pluginName"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --Key:Enter}}", actual: rules[0]["argument"].GetValue<string>());
            Assert.AreEqual(expected: expectedLocator, actual: rules[0]["onElement"]?.GetValue<string>());
            Assert.AreEqual(expected: isTargeted, actual: rules[0]["capabilities"] is not null);
        }

        [TestMethod(DisplayName = "Verify a shortcut chord plays through SendUser32KeyboardKey without onElement " +
            "in every UIA mode.")]
        #region *** Data Set ***
        [DataRow("standard")]
        [DataRow("user32")]
        [DataRow("coordinate")]
        #endregion
        public void UiaShortcutTargetTest(string mode)
        {
            // Arrange: Ctrl+A reported as a chord on a recorded element.
            var events = new[]
            {
                NewKeyEvent(eventName: "Key Up", timestamp: 1, key: "A", virtualKey: 0x41, modifiers: ["Ctrl"])
            };

            // Act: convert the stream in the given capture mode.
            var rules = NewUiaRules(events, mode);

            // Assert: the chord targets the focused window, so it names no element.
            Assert.HasCount(expected: 1, collection: rules);
            Assert.AreEqual(expected: "SendUser32KeyboardKey", actual: rules[0]["pluginName"].GetValue<string>());
            Assert.IsNull(value: rules[0]["onElement"]);
            Assert.IsNull(value: rules[0]["capabilities"]);
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
            Assert.IsNull(value: rules[1]["onElement"]);
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
            Assert.IsNull(value: rules[0]["onElement"]);
            Assert.IsNull(value: rules[0]["capabilities"]);
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

        // Creates one Chromium drag and drop as the recorder sends it: the dragged element in the shared chain, the
        // measured distance from its center to the release point, and the drop element's chain in value.target,
        // whose trigger node ranks the given candidates.
        private static JsonObject NewDragEvent(string targetLocator, JsonNode[] targetLocators)
        {
            // Arrange: describe the drop element with its own identifier, strength, and ranked candidates.
            var properties = new JsonObject
            {
                ["locatorId"] = "5b0c6f0e-0000-4000-8000-000000000002",
                ["locatorStrength"] = "Strong",
                ["locators"] = new JsonArray(targetLocators)
            };
            var trigger = new JsonObject
            {
                ["name"] = "Done",
                ["isTriggerElement"] = true,
                ["properties"] = properties
            };
            var target = new JsonObject
            {
                ["chain"] = new JsonObject { ["locator"] = targetLocator, ["path"] = new JsonArray(trigger) }
            };

            // Arrange: carry a fractional distance, which the rule rounds to whole pixels.
            var value = new JsonObject
            {
                ["kind"] = "Native",
                ["deltaX"] = 340.6,
                ["deltaY"] = -40,
                ["target"] = target
            };
            return NewEvent(eventName: "InvokeDragAndDrop", timestamp: 1, value);
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

        // Creates one ranked locator candidate the recorder verified as unique on the page.
        private static JsonObject NewLocator(string @using, string value)
        {
            return new JsonObject
            {
                ["using"] = @using,
                ["value"] = value,
                ["strategy"] = "Label",
                ["score"] = 100,
                ["verdict"] = "Unique"
            };
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

        // Creates one Chromium element event whose target is inside a shadow root: the chain locator reaches only the
        // outermost shadow host, and the trigger node ranks the given locator candidates in the given order.
        private static JsonObject NewShadowEvent(string eventName, JsonObject value, JsonNode[] locators)
        {
            // Arrange: replace the shared chain locator with the host XPath and attach the candidates to the
            // first path node, which is the trigger node of a target-first chain.
            var model = NewEvent(eventName, timestamp: 1, value);
            model["chain"]["locator"] = ShadowHostLocator;
            model["chain"]["path"][0]["properties"] = new JsonObject { ["locators"] = new JsonArray(locators) };
            return model;
        }

        // Creates the locator candidate the Chromium recorder ranks for a target inside a shadow root: a CSS
        // selector that chains one segment per shadow root, with the verdict of the recorder's last re-test.
        private static JsonObject NewShadowLocator(string value, string verdict)
        {
            // Arrange: mirror the recorder contract, where only the value and the verdict vary between targets.
            return new JsonObject
            {
                ["using"] = "CssSelector",
                ["value"] = value,
                ["strategy"] = "ShadowPath",
                ["score"] = 68,
                ["verdict"] = verdict
            };
        }

        // Creates one UIA left-button release past the drag threshold: pressed at (300, 200), released at (450, 180),
        // with the drop element's chain when one was hit, as the UIA recorder adds the drag facts to Left Up.
        private static JsonObject NewUiaDrag(JsonObject target)
        {
            // Arrange: attach the drag facts to the release, leaving the target out when nothing was hit.
            var drag = new JsonObject { ["startX"] = 300, ["startY"] = 200 };
            if (target is not null)
            {
                drag["target"] = target;
            }

            var value = new JsonObject { ["x"] = 450, ["y"] = 180, ["drag"] = drag };
            return NewEvent(eventName: "Left Up", timestamp: 1, value);
        }

        // Converts one User32 capture and returns its rules without the trailing CloseBrowser action.
        private static List<JsonNode> NewUiaRules(JsonObject[] events) => NewUiaRules(events, mode: "user32");

        // Converts one UIA capture in the given mode and returns its rules without the trailing CloseBrowser action.
        private static List<JsonNode> NewUiaRules(JsonObject[] events, string mode)
        {
            // Arrange: use a UIA endpoint in the given capture mode.
            var options = new RecorderModel
            {
                BaseUrl = "http://localhost:9955",
                Mode = mode,
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
