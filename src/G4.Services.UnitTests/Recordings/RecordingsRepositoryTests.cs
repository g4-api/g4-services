using G4.Services.Domain.V4.Recordings;
using G4.Services.Domain.V4.Repositories;
using G4.Settings;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Services.UnitTests.Recordings
{
    [TestClass]
    [TestCategory("RecordingsRepository")]
    [TestCategory("IntegrationTest")]
    public sealed class RecordingsRepositoryTests
    {
        /// <summary>
        /// Gets or sets the runner context whose cancellation token bounds repository calls.
        /// </summary>
        public TestContext TestContext { get; set; }

        [TestMethod(DisplayName = "Verify empty stop returns no automation and releases endpoint ownership.")]
        public async Task EmptyRecordingReleasesEndpointTestAsync()
        {
            // Arrange: connect a passive UIA recorder that emits no events during this session.
            await using var recorder = await TestRecorderServer.NewAsync();
            await using var repository = new RecordingsRepository();
            var settings = NewSettings(NewRecorder(baseUrl: recorder.BaseUrl, driver: "UiaDriver"));
            var status = await repository.StartAsync(settings, cancellationToken: TestContext.CancellationToken);

            // Act: stop an empty recording, retrieve it, and then reuse the same endpoint for a fresh session.
            var stopped = await repository.StopAsync(status.Id);
            var retrieved = repository.GetAutomation(status.Id);
            var next = await repository.StartAsync(settings, cancellationToken: TestContext.CancellationToken);

            // Assert: no synthetic flow is produced, and stop released the endpoint reservation.
            Assert.IsNull(value: stopped);
            Assert.IsNull(value: retrieved);
            Assert.AreNotEqual(notExpected: status.Id, actual: next.Id);
            Assert.AreEqual(expected: 0, actual: recorder.State.StartCount);
        }

        [TestMethod(DisplayName = "Verify failed browser startup releases its endpoint before a new start.")]
        public async Task FailedStartupReleasesEndpointTestAsync()
        {
            // Arrange: the recorder accepts transport connections but rejects the browser launch operation.
            await using var recorder = await TestRecorderServer.NewAsync();
            await using var repository = new RecordingsRepository();
            recorder.State.FailStart = true;
            var settings = NewSettings(NewRecorder(baseUrl: recorder.BaseUrl, driver: "ChromeDriver"));

            // Act: fail startup, then restore the recorder and retry so the retry proves the reservation was released.
            var failure = await Assert.ThrowsExactlyAsync<HttpRequestException>(action: () =>
                repository.StartAsync(settings, cancellationToken: TestContext.CancellationToken));
            recorder.State.FailStart = false;
            var status = await repository.StartAsync(settings, cancellationToken: TestContext.CancellationToken);

            // Assert: startup diagnostics identify the endpoint and the failed attempt leaves no reservation behind.
            Assert.Contains(substring: recorder.BaseUrl, value: failure.Message);
            Assert.AreEqual(expected: 2, actual: recorder.State.StartCount);
            Assert.AreNotEqual(notExpected: Guid.Empty, actual: status.Id);
        }

        [TestMethod(DisplayName = "Verify invalid settings fail before connecting and unknown sessions are not found.")]
        public async Task InvalidSettingsAndUnknownSessionTestAsync()
        {
            // Arrange: no recorder host is necessary because configuration rejection precedes transport allocation.
            await using var repository = new RecordingsRepository();
            var unauthenticated = NewSettings(NewRecorder(baseUrl: "http://localhost:9956", driver: "ChromeDriver"));
            unauthenticated.Authentication = null;
            var scripted = NewRecorder(baseUrl: "http://localhost:9956", driver: "ChromeDriver");
            scripted["preScript"] = new JsonObject { ["enabled"] = true };
            var unsupported = NewRecorder(baseUrl: "http://localhost:9956", driver: "FirefoxDriver");

            // Act: submit missing authentication, active scripts, an unsupported driver, and an unknown identifier.
            var missingAuthentication = await Assert.ThrowsExactlyAsync<ArgumentException>(action: () =>
                repository.StartAsync(unauthenticated, cancellationToken: TestContext.CancellationToken));
            var deferredScript = await Assert.ThrowsExactlyAsync<ArgumentException>(action: () =>
                repository.StartAsync(NewSettings(scripted), cancellationToken: TestContext.CancellationToken));
            var unsupportedDriver = await Assert.ThrowsExactlyAsync<ArgumentException>(action: () =>
                repository.StartAsync(NewSettings(unsupported), cancellationToken: TestContext.CancellationToken));

            // Assert: configuration errors are argument failures, distinct from an unknown retained session.
            Assert.Contains(substring: "Authentication is required.", value: missingAuthentication.Message);
            Assert.Contains(substring: "Pre/post scripts are deferred", value: deferredScript.Message);
            Assert.Contains(substring: "ChromeDriver or UiaDriver", value: unsupportedDriver.Message);
            Assert.ThrowsExactly<KeyNotFoundException>(action: () => repository.GetStatus(Guid.NewGuid()));
            Assert.ThrowsExactly<KeyNotFoundException>(action: () => repository.GetAutomation(Guid.NewGuid()));
        }

        [TestMethod(DisplayName = "Verify malformed recorder settings fail as JSON errors " +
            "before reaching the repository.")]
        public void MalformedSettingsTest()
        {
            // Arrange: replace the recorder array with a string, as a careless client body would.
            var request = new JsonObject
            {
                ["authentication"] = new JsonObject { ["token"] = "" },
                ["recorders"] = "invalid-recorders"
            };

            // Act: bind the body with the Hub's JSON contract, which REST and MCP both rely on.
            var exception = Assert.ThrowsExactly<JsonException>(action: () =>
                request.Deserialize<RecordingSettingsModel>(options: AppSettings.JsonOptions));

            // Assert: malformed nested configuration surfaces as a JSON failure, which the API maps to 400.
            Assert.IsNotNull(value: exception.Message);
        }

        [TestMethod(DisplayName = "Verify interleaved capture, stop, retrieval, endpoint ownership, and driver reuse.")]
        public async Task MultipleRecordersExportAutomationTestAsync()
        {
            // Arrange: independent real SignalR endpoints isolate two Chromium browser lifecycles.
            await using var first = await TestRecorderServer.NewAsync();
            await using var second = await TestRecorderServer.NewAsync();
            await using var repository = new RecordingsRepository();
            var settings = NewSettings(
                NewRecorder(baseUrl: first.BaseUrl, driver: "ChromeDriver"),
                NewRecorder(baseUrl: second.BaseUrl, driver: "ChromeDriver"));
            settings.Authentication = new RecordingAuthenticationModel
            {
                Username = "fixture-user",
                Password = "fixture-password",
                Token = "fixture-token"
            };
            var status = await repository.StartAsync(settings, cancellationToken: TestContext.CancellationToken);

            // Act: verify endpoint ownership and reject automation retrieval before stop.
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(action: () =>
                repository.StartAsync(settings, cancellationToken: TestContext.CancellationToken));
            Assert.ThrowsExactly<InvalidOperationException>(action: () => repository.GetAutomation(status.Id));

            // Act: publish out of delivery order to prove capture timestamps establish A -> B -> A ordering.
            await first.SendAsync(model: NewEvent(plugin: "InvokeClick", timestamp: 300, locator: "//button[@id='last']"));
            await second.SendAsync(model: NewEvent(plugin: "OpenUrl", timestamp: 200, locator: ""));
            await first.SendAsync(model: NewEvent(plugin: "InvokeClick", timestamp: 100, locator: "//button[@id='first']"));
            await WaitForEventsAsync(repository, status.Id, expected: 3);
            var automation = await repository.StopAsync(status.Id);

            // Assert: three jobs preserve chronology, reuse A's driver, and close each endpoint on its final job.
            Assert.IsNotNull(value: automation);
            var jobs = automation["stages"][0]["jobs"].AsArray();
            Assert.HasCount(expected: 3, collection: jobs);
            Assert.AreEqual(expected: "NoDriver", actual: automation["driverParameters"]["driver"].GetValue<string>());
            Assert.AreEqual(expected: "Job(1)", actual: jobs[2]["driverParameters"]["driver"].GetValue<string>());
            Assert.HasCount(expected: 1, collection: jobs[0]["rules"].AsArray());
            Assert.AreEqual(expected: "CloseBrowser", actual: jobs[1]["rules"][1]["pluginName"].GetValue<string>());
            Assert.AreEqual(expected: "CloseBrowser", actual: jobs[2]["rules"][1]["pluginName"].GetValue<string>());

            // Assert: authentication is exported unchanged and no recorder settings leak into the flow.
            var authentication = automation["authentication"];
            Assert.AreEqual(expected: "fixture-user", actual: authentication["username"].GetValue<string>());
            Assert.AreEqual(expected: "fixture-password", actual: authentication["password"].GetValue<string>());
            Assert.AreEqual(expected: "fixture-token", actual: authentication["token"].GetValue<string>());
            Assert.IsNull(value: automation["settings"]);

            // Act: repeated stop and retrieval reuse the retained result rather than invoking the recorders again.
            var repeated = await repository.StopAsync(status.Id);
            var retrieved = repository.GetAutomation(status.Id);

            // Assert: browser stop is tied to each launch and happens exactly once per endpoint.
            Assert.IsTrue(condition: JsonNode.DeepEquals(node1: automation, node2: repeated));
            Assert.IsTrue(condition: JsonNode.DeepEquals(node1: automation, node2: retrieved));
            Assert.AreEqual(expected: 1, actual: first.State.StartCount);
            Assert.AreEqual(expected: 1, actual: first.State.StopCount);
            Assert.AreEqual(expected: 12345, actual: first.State.StoppedProcessId);
            Assert.AreEqual(expected: 1, actual: second.State.StopCount);
            Assert.AreEqual(expected: "ChromeDriver", actual: first.State.DriverParameters["driver"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify stop drains the recorder so a key still on its queue " +
            "reaches the automation.")]
        public async Task StopFlushesPendingEventsTestAsync()
        {
            // Arrange: the UIA recorder holds one typed key that it delivers only while draining its queue.
            await using var recorder = await TestRecorderServer.NewAsync();
            await using var repository = new RecordingsRepository();
            var uiaRecorder = NewRecorder(baseUrl: recorder.BaseUrl, driver: "UiaDriver");
            uiaRecorder["mode"] = "user32";
            var settings = NewSettings(uiaRecorder);
            var status = await repository.StartAsync(settings, cancellationToken: TestContext.CancellationToken);
            var keyValue = new JsonObject { ["key"] = "x", ["virtualKey"] = 0x58, ["modifiers"] = new JsonArray() };
            recorder.State.PendingEvent = new JsonObject
            {
                ["event"] = "Key Up",
                ["type"] = "Keyboard",
                ["timestamp"] = 1,
                ["machineName"] = "fixture-machine",
                ["value"] = keyValue
            };

            // Act: stop without waiting for any delivery first.
            var automation = await repository.StopAsync(status.Id);

            // Assert: the flushed key is exported even though it arrived after the stop request.
            Assert.AreEqual(expected: 1, actual: recorder.State.FlushCount);
            Assert.IsNotNull(value: automation);
            var rule = automation["stages"][0]["jobs"][0]["rules"][0];
            Assert.AreEqual(expected: "SendUser32Keys", actual: rule["pluginName"].GetValue<string>());
            Assert.AreEqual(expected: "{{$ --Keys:x}}", actual: rule["argument"].GetValue<string>());
        }

        [TestMethod(DisplayName = "Verify a suspended recorder drops events until resumed and reports its state.")]
        public async Task SuspendResumeTestAsync()
        {
            // Arrange: one UIA recorder in a running session.
            await using var recorder = await TestRecorderServer.NewAsync();
            await using var repository = new RecordingsRepository();
            var settings = NewSettings(NewRecorder(baseUrl: recorder.BaseUrl, driver: "UiaDriver"));
            var status = await repository.StartAsync(settings, cancellationToken: TestContext.CancellationToken);
            var configuredUrl = recorder.BaseUrl + "/";

            // Act: suspend through the configured spelling, send an event, then resume and send another.
            var suspended = repository.SuspendRecorder(status.Id, baseUrl: configuredUrl);
            await recorder.SendAsync(model: NewEvent(plugin: "InvokeClick", timestamp: 1, locator: "//dropped"));
            await Task.Delay(millisecondsDelay: 200, cancellationToken: TestContext.CancellationToken);
            var resumed = repository.ResumeRecorder(status.Id, baseUrl: recorder.BaseUrl);
            await recorder.SendAsync(model: NewEvent(plugin: "InvokeClick", timestamp: 2, locator: "//kept"));
            await WaitForEventsAsync(repository, status.Id, expected: 1);

            // Assert: suspension is reported and only the event sent after resume is retained.
            Assert.IsTrue(condition: suspended.Recorders[0].Suspended);
            Assert.IsFalse(condition: resumed.Recorders[0].Suspended);
            Assert.AreEqual(expected: 1, actual: repository.GetStatus(status.Id).Recorders[0].EventCount);
        }

        [TestMethod(DisplayName = "Verify suspend rejects unknown recorders and stopped sessions.")]
        public async Task SuspendFailureTestAsync()
        {
            // Arrange: one UIA recorder in a running session.
            await using var recorder = await TestRecorderServer.NewAsync();
            await using var repository = new RecordingsRepository();
            var settings = NewSettings(NewRecorder(baseUrl: recorder.BaseUrl, driver: "UiaDriver"));
            var status = await repository.StartAsync(settings, cancellationToken: TestContext.CancellationToken);

            // Act: target a foreign endpoint, then stop and target the session's own endpoint.
            var unknown = Assert.ThrowsExactly<ArgumentException>(action: () =>
                repository.SuspendRecorder(status.Id, baseUrl: "http://localhost:1"));
            await repository.StopAsync(status.Id);
            var stopped = Assert.ThrowsExactly<InvalidOperationException>(action: () =>
                repository.SuspendRecorder(status.Id, baseUrl: recorder.BaseUrl));

            // Assert: each failure names its cause so REST maps them to 400 and 409.
            Assert.Contains(substring: "not part of this recording session", value: unknown.Message);
            Assert.Contains(substring: "stopped", value: stopped.Message);
        }

        [TestMethod(DisplayName = "Verify status after stop maps each job to its recorder and exclusions apply.")]
        public async Task StopStatusJobsTestAsync()
        {
            // Arrange: a session that excludes one process, with one excluded and one kept click.
            await using var recorder = await TestRecorderServer.NewAsync();
            await using var repository = new RecordingsRepository();
            var settings = NewSettings(NewRecorder(baseUrl: recorder.BaseUrl, driver: "UiaDriver"));
            settings.ExcludedProcessIds = [4242];
            var status = await repository.StartAsync(settings, cancellationToken: TestContext.CancellationToken);
            var excluded = NewUiaClick(timestamp: 1, processId: 4242);
            var kept = NewUiaClick(timestamp: 2, processId: 7);

            // Act: deliver both clicks, stop, and read the final status.
            await recorder.SendAsync(model: excluded);
            await recorder.SendAsync(model: kept);
            await WaitForEventsAsync(repository, status.Id, expected: 2);
            var automation = await repository.StopAsync(status.Id);
            var finalStatus = repository.GetStatus(status.Id);

            // Assert: the excluded click is gone, and the single job is mapped to the recorder.
            var rules = automation["stages"][0]["jobs"][0]["rules"].AsArray();
            Assert.HasCount(expected: 2, collection: rules);
            Assert.HasCount(expected: 1, collection: finalStatus.Jobs);
            Assert.AreEqual(expected: recorder.BaseUrl, actual: finalStatus.Jobs[0].BaseUrl);
            Assert.AreEqual(expected: 0, actual: finalStatus.Jobs[0].Index);

            // Assert: the first job's zero index survives the Hub's default-omitting JSON options.
            var statusJson = JsonSerializer.Serialize(value: finalStatus, options: AppSettings.JsonOptions);
            Assert.Contains(substring: "\"index\":0", value: statusJson);
        }

        [TestMethod(DisplayName = "Verify one failed recorder leaves its successful peer recording and exportable.")]
        public async Task PartialStartupPreservesSuccessfulRecorderTestAsync()
        {
            // Arrange: two endpoints are reachable, but only one can launch its browser.
            await using var healthy = await TestRecorderServer.NewAsync();
            await using var failed = await TestRecorderServer.NewAsync();
            await using var repository = new RecordingsRepository();
            failed.State.FailStart = true;
            var settings = NewSettings(
                NewRecorder(baseUrl: healthy.BaseUrl, driver: "ChromeDriver"),
                NewRecorder(baseUrl: failed.BaseUrl, driver: "ChromeDriver"));
            var started = await repository.StartAsync(settings, cancellationToken: TestContext.CancellationToken);

            // Act: capture and stop through the healthy peer while retaining diagnostics for its failed sibling.
            await healthy.SendAsync(model: NewEvent(plugin: "InvokeClick", timestamp: 1, locator: "//button"));
            await WaitForEventsAsync(repository, started.Id, expected: 1);
            var automation = await repository.StopAsync(started.Id);

            // Assert: partial startup returns usable capture and the failed endpoint never receives browser stop.
            Assert.IsTrue(condition: started.Recorders[0].Connected);
            Assert.IsFalse(condition: started.Recorders[1].Connected);
            Assert.IsNotNull(value: started.Recorders[1].Error);
            Assert.IsNotNull(value: automation);
            Assert.AreEqual(expected: 1, actual: healthy.State.StopCount);
            Assert.AreEqual(expected: 0, actual: failed.State.StopCount);
        }

        // Supplies the Chromium event model inside the envelope assembled by the controlled recorder host.
        private static JsonObject NewEvent(string plugin, long timestamp, string locator)
        {
            // Arrange: carry a URL value so the same model serves both navigation and element events.
            return new JsonObject
            {
                ["event"] = plugin,
                ["timestamp"] = timestamp,
                ["machineName"] = "fixture-machine",
                ["chain"] = new JsonObject { ["locator"] = locator },
                ["value"] = new JsonObject { ["url"] = "https://example.test" }
            };
        }

        // Creates one UIA left-click release whose top window belongs to the given process.
        private static JsonObject NewUiaClick(long timestamp, int processId)
        {
            // Arrange: carry a targeted chain so the click converts into an element action.
            var chain = new JsonObject
            {
                ["locator"] = "//target",
                ["path"] = new JsonArray(new JsonObject { ["name"] = "target" }),
                ["topWindow"] = new JsonObject { ["processId"] = processId, ["name"] = "Window" }
            };

            return new JsonObject
            {
                ["event"] = "Left Up",
                ["type"] = "Mouse",
                ["timestamp"] = timestamp,
                ["machineName"] = "fixture-machine",
                ["chain"] = chain,
                ["value"] = new JsonObject { ["x"] = 1, ["y"] = 2 }
            };
        }

        // Keeps the recorder's driver dictionary open so transport preservation can be asserted.
        private static JsonObject NewRecorder(string baseUrl, string driver)
        {
            // Arrange: enable the endpoint explicitly, because omitted recorders remain disabled.
            return new JsonObject
            {
                ["enabled"] = true,
                ["baseUrl"] = baseUrl,
                ["driverParameters"] = new JsonObject { ["driver"] = driver }
            };
        }

        // Binds a public settings body with the Hub's JSON contract, as the REST controller and MCP tool receive it.
        private static RecordingSettingsModel NewSettings(params JsonObject[] recorders)
        {
            // Arrange: build the wire body without sharing parented JSON nodes across requests.
            var request = new JsonObject
            {
                ["authentication"] = new JsonObject { ["token"] = "" },
                ["recorders"] = new JsonArray(recorders.Select(selector: item => (JsonNode)item).ToArray())
            };

            return request.Deserialize<RecordingSettingsModel>(options: AppSettings.JsonOptions);
        }

        // Waits on an observable event count with a deadline rather than assuming socket delivery timing.
        private static async Task WaitForEventsAsync(RecordingsRepository repository, Guid identifier, int expected)
        {
            // Arrange: bound eventual delivery so a broken transport produces a test failure instead of a hang.
            using var timeout = new CancellationTokenSource(delay: TimeSpan.FromSeconds(seconds: 5));
            while (true)
            {
                // Act: poll observable state instead of assuming a fixed SignalR delivery delay.
                var count = repository.GetStatus(identifier).Recorders.Sum(selector: item => item.EventCount);

                // Assert: finish only after the whole expected batch is visible to conversion.
                if (count == expected)
                {
                    return;
                }

                // Act: yield between polls while retaining the shared deadline for stalled delivery.
                await Task.Delay(millisecondsDelay: 20, cancellationToken: timeout.Token);
            }
        }
    }
}
