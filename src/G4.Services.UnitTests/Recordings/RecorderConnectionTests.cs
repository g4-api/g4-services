using G4.Services.Domain.V4.Recordings;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Services.UnitTests.Recordings
{
    [TestClass]
    [TestCategory("RecorderConnection")]
    [TestCategory("UnitTest")]
    public sealed class RecorderConnectionTests
    {
        // Holds the chain locator every amendment in these tests reports, which becomes the rule's onElement.
        private const string AmendedLocator = "//button[@aria-label='Save']";

        // Identifies the element whose locators the amendments in these tests correct.
        private const string FirstLocatorId = "5b0c6f0e-0000-4000-8000-000000000001";

        // Identifies a second element, which no amendment in these tests names.
        private const string SecondLocatorId = "5b0c6f0e-0000-4000-8000-000000000002";

        /// <summary>
        /// Gets or sets the runner context whose cancellation token bounds the transport calls.
        /// </summary>
        public TestContext TestContext { get; set; }

        [TestMethod(DisplayName = "Verify a broadcast locator amendment corrects a delivered Chromium event, " +
            "also while the recorder is suspended, without counting as an event.")]
        public async Task LocatorAmendmentBroadcastTestAsync()
        {
            // Arrange: a Chromium recorder on a real SignalR socket delivers one click with a weak locator.
            await using var recorder = await TestRecorderServer.NewAsync();
            var options = new RecorderModel
            {
                BaseUrl = recorder.BaseUrl,
                DriverParameters = new Dictionary<string, object> { ["driver"] = "ChromeDriver" }
            };
            await using var connection = new RecorderConnection(options);
            await connection.StartAsync(cancellationToken: TestContext.CancellationToken);
            await recorder.SendAsync(model: NewEvent(locatorId: FirstLocatorId, timestamp: 1));
            await WaitForEventsAsync(connection, expected: 1);

            // Act: amend while suspended, and leave the transport time to deliver the amendment during the pause.
            var amendment = NewAmendment(locatorId: FirstLocatorId);
            connection.Suspend();
            await recorder.SendAmendmentAsync(amendment);
            await Task.Delay(millisecondsDelay: 200, cancellationToken: TestContext.CancellationToken);
            connection.Resume();

            // Act: the client runs its handlers in arrival order, so a buffered sentinel event proves the amendment
            // sent before it has been handled.
            await recorder.SendAsync(model: NewEvent(locatorId: SecondLocatorId, timestamp: 2));
            await WaitForEventsAsync(connection, expected: 2);
            var events = connection.StopBuffer();
            var automation = AutomationBuilder.NewAutomation(
                authentication: new RecordingAuthenticationModel { Token = "" },
                captures: [new RecorderCapture(Options: options, Events: events)]);
            var rules = automation["stages"][0]["jobs"][0]["rules"].AsArray();

            // Assert: the amendment is not buffered as an event, and the amended click exports its corrections.
            var capabilities = rules[0]["capabilities"];
            var isLocatorsAmended = JsonNode.DeepEquals(node1: amendment["locators"], node2: capabilities["locators"]);
            Assert.HasCount(expected: 2, collection: events);
            Assert.AreEqual(expected: AmendedLocator, actual: rules[0]["onElement"].GetValue<string>());
            Assert.AreEqual(expected: "Strong", actual: capabilities["locatorStrength"].GetValue<string>());
            Assert.IsTrue(condition: isLocatorsAmended);

            // Assert: the sentinel click keeps the locator and strength it was recorded with.
            var sentinelStrength = rules[1]["capabilities"]["locatorStrength"].GetValue<string>();
            Assert.AreEqual(expected: "//recorded", actual: rules[1]["onElement"].GetValue<string>());
            Assert.AreEqual(expected: "Weak", actual: sentinelStrength);
        }

        [TestMethod(DisplayName = "Verify a locator amendment that is malformed, or names no buffered event, " +
            "changes nothing.")]
        #region *** Data Set ***
        [DataRow("{\"locatorId\":\"unknown-id\",\"locator\":\"//amended\",\"locatorStrength\":\"Strong\"}")]
        [DataRow("{\"locator\":\"//amended\",\"locatorStrength\":\"Strong\"}")]
        [DataRow("{\"locatorId\":\"\",\"locator\":\"//amended\"}")]
        [DataRow("{\"locatorId\":7,\"locator\":\"//amended\"}")]
        [DataRow("{\"locatorId\":[\"" + FirstLocatorId + "\"],\"locator\":\"//amended\"}")]
        [DataRow("[\"" + FirstLocatorId + "\"]")]
        [DataRow("\"" + FirstLocatorId + "\"")]
        [DataRow("null")]
        #endregion
        public void UpdateLocatorsIgnoredAmendmentTest(string amendmentJson)
        {
            // Arrange: two buffered events, and an amendment payload that identifies neither of them.
            var events = new[]
            {
                NewEvent(locatorId: FirstLocatorId, timestamp: 1),
                NewEvent(locatorId: SecondLocatorId, timestamp: 2)
            };
            var expected = events.Select(selector: model => model.ToJsonString()).ToArray();
            var amendment = JsonNode.Parse(json: amendmentJson);

            // Act: apply the payload exactly as the receive handler passes the envelope value.
            var updated = RecorderConnection.UpdateLocators(events, amendment);

            // Assert: nothing matches, nothing throws, and both events keep their recorded text.
            var actual = events.Select(selector: model => model.ToJsonString()).ToArray();
            Assert.AreEqual(expected: 0, actual: updated);
            Assert.AreSequenceEqual(expected: expected, actual: actual);
        }

        [TestMethod(DisplayName = "Verify a locator amendment updates the locator, candidates, and strength " +
            "of the matching event only.")]
        public void UpdateLocatorsMatchingEventTest()
        {
            // Arrange: two buffered events with their own identifiers, and an amendment that names the first.
            var first = NewEvent(locatorId: FirstLocatorId, timestamp: 1);
            var second = NewEvent(locatorId: SecondLocatorId, timestamp: 2);
            var secondText = second.ToJsonString();
            var amendment = NewAmendment(locatorId: FirstLocatorId);
            var expectedLocators = amendment["locators"].DeepClone();

            // Act: apply the amendment, then change the amendment's own candidates to expose any shared node.
            var updated = RecorderConnection.UpdateLocators(events: [first, second], amendment);
            amendment["locators"].AsArray().Clear();

            // Assert: the first event takes the three corrections and owns a detached copy of the candidates.
            var properties = first["chain"]["path"][0]["properties"];
            Assert.AreEqual(expected: 1, actual: updated);
            Assert.AreEqual(expected: AmendedLocator, actual: first["chain"]["locator"].GetValue<string>());
            Assert.AreEqual(expected: "Strong", actual: properties["locatorStrength"].GetValue<string>());
            Assert.IsTrue(condition: JsonNode.DeepEquals(node1: expectedLocators, node2: properties["locators"]));

            // Assert: members the amendment does not carry survive, and the second event is unchanged.
            Assert.AreEqual(expected: FirstLocatorId, actual: properties["locatorId"].GetValue<string>());
            Assert.AreEqual(expected: "button", actual: properties["tagName"].GetValue<string>());
            Assert.AreEqual(expected: secondText, actual: second.ToJsonString());
        }

        [TestMethod(DisplayName = "Verify every event that shares the identifier is amended through its flagged " +
            "trigger node, or its first node when none is flagged.")]
        public void UpdateLocatorsTriggerNodeTest()
        {
            // Arrange: the first event flags its second path node as the trigger, while its first node carries
            // another identifier; the second event flags nothing, so its first node is the trigger.
            var flagged = NewEvent(locatorId: SecondLocatorId, timestamp: 1);
            var trigger = NewEvent(locatorId: FirstLocatorId, timestamp: 1)["chain"]["path"][0].DeepClone();
            flagged["chain"]["path"][0].AsObject().Remove(propertyName: "isTriggerElement");
            flagged["chain"]["path"].AsArray().Add(item: trigger);
            var unflagged = NewEvent(locatorId: FirstLocatorId, timestamp: 2);
            unflagged["chain"]["path"][0].AsObject().Remove(propertyName: "isTriggerElement");

            // Arrange: events that target no element, or carry a malformed chain, sit between the two.
            var navigation = new JsonObject { ["event"] = "OpenUrl", ["timestamp"] = 3 };
            var textChain = new JsonObject { ["event"] = "InvokeClick", ["chain"] = "//text" };
            var emptyPath = new JsonObject { ["chain"] = new JsonObject { ["path"] = new JsonArray() } };
            var textNode = new JsonObject { ["chain"] = new JsonObject { ["path"] = new JsonArray("//text") } };
            JsonObject[] events = [flagged, navigation, textChain, emptyPath, textNode, unflagged];

            // Act: apply one amendment for the shared identifier.
            var amendment = NewAmendment(locatorId: FirstLocatorId);
            var updated = RecorderConnection.UpdateLocators(events, amendment);

            // Assert: both targeted events are amended, each on the node resolved as its trigger.
            var flaggedPath = flagged["chain"]["path"];
            var triggerStrength = flaggedPath[1]["properties"]["locatorStrength"].GetValue<string>();
            var firstNodeStrength = flaggedPath[0]["properties"]["locatorStrength"].GetValue<string>();
            Assert.AreEqual(expected: 2, actual: updated);
            Assert.AreEqual(expected: "Strong", actual: triggerStrength);
            Assert.AreEqual(expected: "Weak", actual: firstNodeStrength);
            Assert.AreEqual(expected: AmendedLocator, actual: unflagged["chain"]["locator"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify a matching locator amendment without usable values leaves " +
            "the event as recorded.")]
        #region *** Data Set ***
        [DataRow("\"reason\":\"Settle\",\"timestamp\":1730000000000")]
        [DataRow("\"locator\":\"\",\"locators\":null,\"locatorStrength\":\"\"")]
        [DataRow("\"locator\":5,\"locators\":\"//x\",\"locatorStrength\":[]")]
        #endregion
        public void UpdateLocatorsUnusableValuesTest(string members)
        {
            // Arrange: one buffered event, and an amendment that names it with absent, empty, or mistyped values.
            var model = NewEvent(locatorId: FirstLocatorId, timestamp: 1);
            var expected = model.ToJsonString();
            var amendmentJson = "{\"locatorId\":\"" + FirstLocatorId + "\"," + members + "}";
            var amendment = JsonNode.Parse(json: amendmentJson);

            // Act: apply the amendment to the event it identifies.
            var updated = RecorderConnection.UpdateLocators(events: [model], amendment);

            // Assert: the event is matched, yet every recorded value is kept because no correction is usable.
            Assert.AreEqual(expected: 1, actual: updated);
            Assert.AreEqual(expected: expected, actual: model.ToJsonString());
        }

        // Creates the amendment the Chromium recorder broadcasts after it re-tests an element's locators: a stronger
        // chain locator, one replacement candidate, and the new strength.
        private static JsonObject NewAmendment(string locatorId)
        {
            // Arrange: replace the recorded candidates with a single accessible-name XPath.
            var candidate = new JsonObject
            {
                ["using"] = "Xpath",
                ["value"] = AmendedLocator,
                ["strategy"] = "AriaLabel",
                ["score"] = 95,
                ["verdict"] = "Unique"
            };

            return new JsonObject
            {
                ["locatorId"] = locatorId,
                ["locator"] = AmendedLocator,
                ["locators"] = new JsonArray(candidate),
                ["locatorStrength"] = "Strong",
                ["reason"] = "Settle",
                ["timestamp"] = 1730000000000
            };
        }

        // Creates one Chromium click whose flagged trigger node carries a weak positional locator under the given
        // locator identifier, as the recorder sends it before any amendment.
        private static JsonObject NewEvent(string locatorId, long timestamp)
        {
            // Arrange: record a single positional candidate, which a later amendment can replace.
            var candidate = new JsonObject
            {
                ["using"] = "Xpath",
                ["value"] = "//recorded",
                ["strategy"] = "Position",
                ["score"] = 20,
                ["verdict"] = "Unique"
            };
            var properties = new JsonObject
            {
                ["tagName"] = "button",
                ["locatorId"] = locatorId,
                ["locatorStrength"] = "Weak",
                ["locators"] = new JsonArray(candidate)
            };

            // Arrange: flag the first path node as the trigger, matching the recorder's target-first chain.
            var trigger = new JsonObject
            {
                ["name"] = "Save",
                ["isTriggerElement"] = true,
                ["properties"] = properties
            };

            return new JsonObject
            {
                ["event"] = "InvokeClick",
                ["timestamp"] = timestamp,
                ["machineName"] = "fixture-machine",
                ["value"] = new JsonObject(),
                ["chain"] = new JsonObject { ["locator"] = "//recorded", ["path"] = new JsonArray(trigger) }
            };
        }

        // Waits on the connection's observable event count with a deadline rather than assuming delivery timing.
        private static async Task WaitForEventsAsync(RecorderConnection connection, int expected)
        {
            // Arrange: bound eventual delivery so a broken transport produces a test failure instead of a hang.
            using var timeout = new CancellationTokenSource(delay: TimeSpan.FromSeconds(seconds: 5));
            while (connection.GetStatus().EventCount != expected)
            {
                // Act: yield between polls while retaining the shared deadline for stalled delivery.
                await Task.Delay(millisecondsDelay: 20, cancellationToken: timeout.Token);
            }
        }
    }
}
