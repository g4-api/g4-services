using G4.Services.Domain.V4.Extensions;
using G4.Services.Domain.V4.Mcp;
using G4.Services.Domain.V4.Recordings;
using G4.Settings;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Services.UnitTests.Recordings
{
    [TestClass]
    [TestCategory("McpEndpoint")]
    [TestCategory("IntegrationTest")]
    public sealed class McpEndpointTests
    {
        /// <summary>
        /// Gets or sets the runner context whose cancellation token bounds test HTTP calls.
        /// </summary>
        public TestContext TestContext { get; set; }

        [TestMethod(DisplayName = "Verify catalog tools are reachable only through the router tools " +
            "and never through a direct tools/call.")]
        public async Task McpCatalogRouterOnlyTestAsync()
        {
            // Arrange: host the production registration whose fake catalog records every forwarded envelope.
            await using var server = await TestMcpServer.NewAsync();
            using var client = server.NewClient();
            var intent = new { agentIntent = "run the fixture tool", userIntent = "run it" };
            var rule = new { toolName = FakeToolsRepository.ToolName, toolParameters = new { value = "hello" } };
            var arguments = new { driverSession = "s-1", token = "token", intent, rule };

            // Act: call the catalog tool by name, then run the same tool through the g4.SendRule router.
            var direct = await TestMcpServer.InvokeToolAsync(client, name: FakeToolsRepository.ToolName, arguments: new { });
            var routed = await TestMcpServer.InvokeToolAsync(client, name: "g4.SendRule", arguments);

            // Assert: a direct call is a JSON-RPC invalid-parameter error that never reaches the repository.
            Assert.AreEqual(expected: -32602, actual: direct["error"]["code"].GetValue<int>());
            Assert.HasCount(expected: 1, collection: server.Catalog.Calls);

            // Assert: the router forwards the catalog tool inside the g4.SendRule envelope the repository expects.
            var envelope = server.Catalog.Calls.Single();
            var forwardedRule = envelope.GetProperty(propertyName: "arguments").GetProperty(propertyName: "rule");
            Assert.AreEqual(expected: "g4.SendRule", actual: envelope.GetProperty(propertyName: "name").GetString());
            Assert.AreEqual(expected: FakeToolsRepository.ToolName, actual: forwardedRule.GetProperty("toolName").GetString());

            // Assert: the routed result carries identical JSON text and structured content.
            var content = TestMcpServer.ReadContent(response: routed);
            Assert.IsTrue(condition: JsonNode.DeepEquals(node1: content, node2: routed["result"]["structuredContent"]));
        }

        [TestMethod(DisplayName = "Verify empty recordings and expected lifecycle errors through the recording tools.")]
        public async Task McpEmptyAndFailureTestAsync()
        {
            // Arrange: start through the shared repository to verify MCP resolves sessions created by another transport.
            await using var recorder = await TestRecorderServer.NewAsync();
            await using var server = await TestMcpServer.NewAsync();
            using var client = server.NewClient();
            var request = NewRequest(NewRecorder(baseUrl: recorder.BaseUrl, driver: "UiaDriver"));
            var settings = request.Deserialize<RecordingSettingsModel>(options: AppSettings.JsonOptions);
            var status = await server.Recordings.StartAsync(settings, cancellationToken: TestContext.CancellationToken);
            var arguments = new { id = status.Id };
            var startArguments = new { settings = request };

            // Act: capture domain errors before stopping the otherwise empty session.
            var premature = await TestMcpServer.InvokeToolAsync(client, name: "g4.GetAutomation", arguments);
            var conflict = await TestMcpServer.InvokeToolAsync(client, name: "g4.StartRecording", arguments: startArguments);
            var missing = await TestMcpServer.InvokeToolAsync(
                client,
                name: "g4.GetRecordingStatus",
                arguments: new { id = Guid.NewGuid() });
            var stopped = await TestMcpServer.InvokeToolAsync(client, name: "g4.StopRecording", arguments);

            // Assert: execution failures are MCP tool errors rather than JSON-RPC protocol errors.
            Assert.IsTrue(condition: premature["result"]["isError"].GetValue<bool>());
            Assert.IsTrue(condition: conflict["result"]["isError"].GetValue<bool>());
            Assert.IsTrue(condition: missing["result"]["isError"].GetValue<bool>());

            // Assert: no events produce JSON null text without structured content rather than a fake flow.
            Assert.IsNull(value: TestMcpServer.ReadContent(response: stopped));
            Assert.IsNull(value: stopped["result"]["structuredContent"]);
        }

        [TestMethod(DisplayName = "Verify start rejects settings without authentication with a descriptive tool error.")]
        public async Task McpMissingAuthenticationTestAsync()
        {
            // Arrange: MCP bypasses MVC validation, so the repository must enforce required authentication itself.
            await using var server = await TestMcpServer.NewAsync();
            using var client = server.NewClient();
            var request = NewRequest(NewRecorder(baseUrl: "http://localhost:9956", driver: "ChromeDriver"));
            request.Remove(propertyName: "authentication");
            var arguments = new { settings = request };

            // Act: start without authentication through the MCP tool.
            var response = await TestMcpServer.InvokeToolAsync(client, name: "g4.StartRecording", arguments);

            // Assert: the failure is a tool error naming the missing field.
            Assert.IsTrue(condition: response["result"]["isError"].GetValue<bool>());
            var failureText = response["result"]["content"][0]["text"].GetValue<string>();
            Assert.Contains(substring: "Authentication is required.", value: failureText);
        }

        [TestMethod(DisplayName = "Verify initialization, attribute tool discovery, ping, notifications, " +
            "and protocol errors.")]
        public async Task McpProtocolTestAsync()
        {
            // Arrange: exercise the actual SDK HTTP handler with the established initialize-based protocol.
            await using var server = await TestMcpServer.NewAsync();
            using var client = server.NewClient();
            var parameters = new
            {
                protocolVersion = TestMcpServer.ProtocolVersion,
                capabilities = new { },
                clientInfo = new { name = "g4-services-tests", version = "1.0" }
            };

            // Act: initialize and discover tools without any recorder service or plugin cache.
            var initialized = await TestMcpServer.InvokeAsync(client, method: "initialize", parameters);
            var catalog = await TestMcpServer.InvokeAsync(client, method: "tools/list", parameters: new { });
            var names = catalog["result"]["tools"]
                .AsArray()
                .Select(selector: tool => tool["name"].GetValue<string>())
                .ToArray();

            // Assert: the server identifies itself and does not advertise notifications a stateless server cannot send.
            var result = initialized["result"];
            var protocolVersion = result["protocolVersion"].GetValue<string>();
            var serverName = result["serverInfo"]["name"].GetValue<string>();
            Assert.AreEqual(expected: TestMcpServer.ProtocolVersion, actual: protocolVersion);
            Assert.AreEqual(expected: McpServiceExtensions.ServerName, actual: serverName);
            Assert.IsFalse(condition: result["capabilities"]["tools"]["listChanged"]?.GetValue<bool>() ?? false);

            // Assert: tools/list advertises exactly the attribute system and recording tools, ordered by name.
            var definitions = McpToolTypes.NewDefinitions();
            var expected = definitions.Select(selector: tool => tool.Name).ToArray();
            Assert.AreSequenceEqual(expected, actual: names, order: SequenceOrder.InAnyOrder);
            Assert.HasCount(expected: 21, collection: names);

            // Assert: catalog tools are never advertised; the router tools are their only entry point.
            Assert.DoesNotContain(notExpected: FakeToolsRepository.ToolName, collection: names);

            // Assert: the catalog definitions come from the same SDK schema builder that answers tools/list.
            foreach (var definition in definitions)
            {
                var listed = catalog["result"]["tools"]
                    .AsArray()
                    .Single(predicate: tool => tool["name"].GetValue<string>() == definition.Name);
                var generated = JsonSerializer.SerializeToNode(value: definition.InputSchema);
                Assert.IsTrue(condition: JsonNode.DeepEquals(node1: generated, node2: listed["inputSchema"]), message: definition.Name);
            }

            // Assert: the start tool exposes the REST settings body as its input contract.
            var start = catalog["result"]["tools"]
                .AsArray()
                .Single(predicate: tool => tool["name"].GetValue<string>() == "g4.StartRecording");
            Assert.IsNotNull(value: start["inputSchema"]["properties"]["settings"]);

            // Act: probe a supported control method and an unknown method without invoking any tool.
            var ping = await TestMcpServer.InvokeAsync(client, method: "ping", parameters: new { });
            var unknown = await TestMcpServer.InvokeAsync(client, method: "unknown-method", parameters: new { });

            // Assert: ping succeeds and unsupported methods retain the JSON-RPC method-not-found code.
            Assert.IsNotNull(value: ping["result"]);
            Assert.AreEqual(expected: -32601, actual: unknown["error"]["code"].GetValue<int>());

            // Act: notifications carry no identifier and receive an empty HTTP acknowledgment.
            var initializedNotification = new { jsonrpc = "2.0", method = "notifications/initialized" };
            using var notification = TestMcpServer.NewRequest(content: initializedNotification);
            using var response = await client.SendAsync(notification, cancellationToken: TestContext.CancellationToken);

            // Assert: notifications acknowledge receipt without inventing a JSON-RPC response identifier.
            Assert.AreEqual(expected: HttpStatusCode.Accepted, actual: response.StatusCode);
        }

        [TestMethod(DisplayName = "Verify multi-recorder capture, structured results, " +
            "transport interoperability, and retained automation.")]
        public async Task McpRecordingLifecycleTestAsync()
        {
            // Arrange: two independent recorder hosts prove that MCP forwards the complete settings.
            await using var first = await TestRecorderServer.NewAsync();
            await using var second = await TestRecorderServer.NewAsync();
            await using var server = await TestMcpServer.NewAsync();
            using var client = server.NewClient();
            var request = NewRequest(
                NewRecorder(baseUrl: first.BaseUrl, driver: "ChromeDriver"),
                NewRecorder(baseUrl: second.BaseUrl, driver: "ChromeDriver"));
            var startArguments = new { settings = request };

            // Act: start via MCP and observe the same identifier through the shared repository.
            var started = await TestMcpServer.InvokeToolAsync(client, name: "g4.StartRecording", arguments: startArguments);
            var status = TestMcpServer.ReadContent(response: started);
            var identifier = status["id"].GetValue<Guid>();
            Assert.HasCount(expected: 2, collection: status["recorders"].AsArray());
            Assert.IsTrue(condition: JsonNode.DeepEquals(node1: status, node2: started["result"]["structuredContent"]));

            // Act: publish through both peers and wait until the repository observes both event buffers.
            await first.SendAsync(model: NewEvent(plugin: "InvokeClick", timestamp: 1, locator: "//first"));
            await second.SendAsync(model: NewEvent(plugin: "InvokeClick", timestamp: 2, locator: "//second"));
            await WaitForEventsAsync(server, identifier, expected: 2);

            // Act: stop and retrieve through MCP, then compare with the repository's own representation.
            var arguments = new { id = identifier };
            var stopped = await TestMcpServer.InvokeToolAsync(client, name: "g4.StopRecording", arguments);
            var retrieved = await TestMcpServer.InvokeToolAsync(client, name: "g4.GetAutomation", arguments);
            var repeated = await TestMcpServer.InvokeToolAsync(client, name: "g4.StopRecording", arguments);
            var automation = server.Recordings.GetAutomation(identifier);
            var stoppedContent = TestMcpServer.ReadContent(response: stopped);
            var retrievedContent = TestMcpServer.ReadContent(response: retrieved);
            var repeatedContent = TestMcpServer.ReadContent(response: repeated);

            // Assert: both transports expose identical flows and repeated stop closes each browser only once.
            Assert.IsTrue(condition: JsonNode.DeepEquals(node1: automation, node2: stoppedContent));
            Assert.IsTrue(condition: JsonNode.DeepEquals(node1: automation, node2: retrievedContent));
            Assert.IsTrue(condition: JsonNode.DeepEquals(node1: automation, node2: repeatedContent));
            Assert.IsTrue(condition: JsonNode.DeepEquals(node1: automation, node2: stopped["result"]["structuredContent"]));
            Assert.HasCount(expected: 2, collection: automation["stages"][0]["jobs"].AsArray());
            Assert.AreEqual(expected: 1, actual: first.State.StopCount);
            Assert.AreEqual(expected: 1, actual: second.State.StopCount);

            // Act: inspect lifecycle state after cleanup through the remaining status tool.
            var finalStatus = await TestMcpServer.InvokeToolAsync(client, name: "g4.GetRecordingStatus", arguments);

            // Assert: the session reports the terminal stopped state.
            var finalState = TestMcpServer.ReadContent(response: finalStatus)["state"].GetValue<string>();
            Assert.AreEqual(expected: "stopped", actual: finalState);
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

        // Mirrors the public settings body without sharing parented JSON nodes across requests.
        private static JsonObject NewRequest(params JsonObject[] recorders)
        {
            // Arrange: reparent the supplied recorders into a fresh array owned by this request body.
            return new JsonObject
            {
                ["authentication"] = new JsonObject { ["token"] = "" },
                ["recorders"] = new JsonArray(recorders.Select(selector: item => (JsonNode)item).ToArray())
            };
        }

        // Waits on an observable event count with a deadline rather than assuming socket delivery timing.
        private static async Task WaitForEventsAsync(TestMcpServer server, Guid identifier, int expected)
        {
            // Arrange: bound eventual delivery so a broken transport produces a test failure instead of a hang.
            using var timeout = new CancellationTokenSource(delay: TimeSpan.FromSeconds(seconds: 5));
            while (true)
            {
                // Act: poll the shared repository instead of assuming a fixed SignalR delivery delay.
                var count = server.Recordings.GetStatus(identifier).Recorders.Sum(selector: item => item.EventCount);

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
