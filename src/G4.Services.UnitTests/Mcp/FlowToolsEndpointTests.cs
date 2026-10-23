using G4.Services.Domain.V4.Repositories;
using G4.Services.UnitTests.Recordings;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace G4.Services.UnitTests.Mcp
{
    [TestClass]
    [TestCategory("McpEndpoint")]
    [TestCategory("IntegrationTest")]
    public sealed class FlowToolsEndpointTests
    {
        [TestMethod(DisplayName = "Verify flow tools map unknown flows, invalid flows, data source errors, " +
            "and identity conflicts to tool errors.")]
        public async Task FlowFailureTestAsync()
        {
            // Arrange: host the production registration over the in-memory flows client and its reserved keys.
            await using var server = await TestMcpServer.NewAsync();
            using var client = server.NewClient();
            var automation = new { stages = Array.Empty<object>() };
            var conflictArguments = new { key = FakeFlowsClient.ConflictKey, automation };

            // Act: trigger each failure the flows client reports.
            var missingArguments = new { key = "Missing" };
            var conflict = await TestMcpServer.InvokeToolAsync(client, name: "g4.RegisterFlow", conflictArguments);
            var invalidFlow = await InvokeFlowAsync(client, key: FakeFlowsClient.InvalidFlowKey);
            var invalidDataSource = await InvokeFlowAsync(client, key: FakeFlowsClient.InvalidDataSourceKey);
            var missing = await TestMcpServer.InvokeToolAsync(client, name: "g4.RemoveFlow", missingArguments);

            // Assert: each failure is a tool error whose message tells the caller what to correct.
            Assert.Contains(substring: FakeFlowsClient.ConflictKey, value: ReadError(response: conflict));
            var missingIdentity = $"'{FlowsRepository.DefaultNamespace}.Missing'";
            Assert.Contains(substring: "not valid JSON after its parameters", value: ReadError(response: invalidFlow));
            Assert.Contains(substring: "fixture.csv", value: ReadError(response: invalidDataSource));
            Assert.Contains(substring: missingIdentity, value: ReadError(response: missing));
            Assert.IsEmpty(collection: server.Flows.Manifests);
        }

        [TestMethod(DisplayName = "Verify register, get, list, invoke, stop, and remove through the flow tools.")]
        public async Task FlowLifecycleTestAsync()
        {
            // Arrange: register a native automation with one declared parameter, as g4.GetAutomation returns it.
            await using var server = await TestMcpServer.NewAsync();
            using var client = server.NewClient();
            var driverParameters = new { driver = "ChromeDriver" };
            var automation = new { stages = Array.Empty<object>(), driverParameters };
            var parameters = new[] { new { name = "UserName", @default = "admin", mandatory = true } };
            var registerArguments = new { key = "Login", automation, summary = "Logs in.", parameters };
            var invokeParameters = new { UserName = "root" };
            var invokeArguments = new { key = "Login", parameters = invokeParameters, referenceId = "run-1" };
            var flowArguments = new { key = "Login" };
            var stopArguments = new { automationId = "run-1" };

            // Act: run the complete lifecycle through MCP.
            var registered = await TestMcpServer.InvokeToolAsync(client, name: "g4.RegisterFlow", registerArguments);
            var fetched = await TestMcpServer.InvokeToolAsync(client, name: "g4.GetFlow", flowArguments);
            var listed = await TestMcpServer.InvokeToolAsync(client, name: "g4.GetFlows", arguments: new { });
            var invoked = await TestMcpServer.InvokeToolAsync(client, name: "g4.InvokeFlow", invokeArguments);
            var stopped = await TestMcpServer.InvokeToolAsync(client, name: "g4.StopFlowInvocation", stopArguments);
            var manifest = server.Flows.Manifests.Values.Single();
            var removed = await TestMcpServer.InvokeToolAsync(client, name: "g4.RemoveFlow", flowArguments);
            var refetched = await TestMcpServer.InvokeToolAsync(client, name: "g4.GetFlow", flowArguments);

            // Assert: registration reports the normalized identity and stores the automation as Base64 JSON.
            var identity = TestMcpServer.ReadContent(response: registered);
            var storedJson = Encoding.UTF8.GetString(bytes: Convert.FromBase64String(s: manifest.Automation));
            var storedAutomation = JsonNode.Parse(json: storedJson);
            var storedDriver = storedAutomation["driverParameters"]["driver"].GetValue<string>();
            var storedNamespace = identity["namespace"].GetValue<string>();
            Assert.AreEqual(expected: FlowsRepository.DefaultNamespace, actual: storedNamespace);
            Assert.AreEqual(expected: "ChromeDriver", actual: storedDriver);

            // Assert: descriptive values and declared parameters reach the stored manifest.
            var storedParameter = manifest.Parameters.Single();
            Assert.AreEqual(expected: "Logs in.", actual: manifest.Summary.Single());
            Assert.AreEqual(expected: "UserName", actual: storedParameter.Name);
            Assert.AreEqual(expected: "admin", actual: storedParameter.Default);
            Assert.IsTrue(condition: storedParameter.Mandatory);

            // Assert: reads return the stored manifest and the counted list.
            var fetchedKey = TestMcpServer.ReadContent(response: fetched)["key"].GetValue<string>();
            var listedCount = TestMcpServer.ReadContent(response: listed)["count"].GetValue<int>();
            Assert.AreEqual(expected: "Login", actual: fetchedKey);
            Assert.AreEqual(expected: 1, actual: listedCount);

            // Assert: invoke binds supplied values only and returns the responses by group identifier.
            var invocation = server.Flows.Invocations.Single();
            Assert.AreEqual(expected: "root", actual: invocation.Parameters["UserName"].ToString());
            Assert.AreEqual(expected: "run-1", actual: invocation.ReferenceId);
            Assert.IsNull(value: invocation.Authentication);
            Assert.IsNotNull(value: TestMcpServer.ReadContent(response: invoked)["fixture-group"]);

            // Assert: stop reports the exit code, and removal makes the flow unknown.
            var stopStatus = TestMcpServer.ReadContent(response: stopped)["status"].GetValue<string>();
            var isRemoved = TestMcpServer.ReadContent(response: removed)["removed"].GetValue<bool>();
            Assert.AreEqual(expected: "stopped", actual: stopStatus);
            Assert.AreEqual(expected: "run-1", actual: server.Flows.Stopped.Single());
            Assert.IsTrue(condition: isRemoved);
            Assert.IsTrue(condition: refetched["result"]["isError"].GetValue<bool>());
        }

        // Invokes one flow by key without parameter values.
        private static Task<JsonNode> InvokeFlowAsync(HttpClient client, string key)
        {
            // Route through the shared MCP helper so invocation shares its framing and correlation checks.
            return TestMcpServer.InvokeToolAsync(client, name: "g4.InvokeFlow", arguments: new { key });
        }

        // Reads the message of a tool error and fails the calling test when the result is not an error.
        private static string ReadError(JsonNode response)
        {
            // Assert: the result is a tool error rather than a successful value or a protocol error.
            Assert.IsTrue(condition: response["result"]["isError"].GetValue<bool>(), message: response.ToJsonString());

            return response["result"]["content"][0]["text"].GetValue<string>();
        }
    }
}
