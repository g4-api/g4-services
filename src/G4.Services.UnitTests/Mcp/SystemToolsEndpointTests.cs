using G4.Services.UnitTests.Recordings;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace G4.Services.UnitTests.Mcp
{
    [TestClass]
    [TestCategory("McpEndpoint")]
    [TestCategory("IntegrationTest")]
    public sealed class SystemToolsEndpointTests
    {
        #region *** Data Set ***
        [DataRow("g4.FindExamples", "intent!,intent.agentIntent!,intent.userIntent!,maxResults!,namespace!,threshold," +
            "toolName!")]
        [DataRow("g4.FindTool", "toolName!")]
        [DataRow("g4.FindTools", "intent!,intent.agentIntent!,intent.userIntent!,take")]
        [DataRow("g4.GetBuffer", "sessionId!")]
        [DataRow("g4.GetFlow", "key!,namespace")]
        [DataRow("g4.GetFlows", "namespace")]
        [DataRow("g4.InvokeFlow", "argument,authentication,driverParameters,key!,namespace,parameters,referenceId," +
            "settings")]
        [DataRow("g4.RegisterCapability", "manifest!")]
        [DataRow("g4.RegisterFlow", "aliases,automation!,categories,description,key!,namespace,parameters," +
            "parameters[].default,parameters[].description,parameters[].mandatory,parameters[].name!,summary")]
        [DataRow("g4.RemoveBuffer", "sessionId!")]
        [DataRow("g4.RemoveFlow", "key!,namespace")]
        [DataRow("g4.RemoveSession", "sessionId!")]
        [DataRow("g4.ResolveLocator", "constraints,constraints.forbid,constraints.mustBeEnabled," +
            "constraints.mustBeVisible,constraints.prefer,driverSession!,intent!,intent.agentIntent!," +
            "intent.userIntent!,openaiApiKey!,openaiModel!,openaiUri!,token!")]
        [DataRow("g4.SendRule", "driverSession!,intent!,intent.agentIntent!,intent.userIntent!,rule!,rule.toolName!," +
            "rule.toolParameters,rule.toolProperties,token!")]
        [DataRow("g4.SendRules", "driverSession!,intent!,intent.agentIntent!,intent.userIntent!,rules!," +
            "rules[].toolName!,rules[].toolParameters,rules[].toolProperties,token!")]
        [DataRow("g4.StartSession", "capabilities,driver!,driverBinaries!,firstMatch,token!")]
        [DataRow("g4.StopFlowInvocation", "automationId!")]
        #endregion
        [TestMethod(DisplayName = "Verify each system and flow tool keeps the argument names and required arguments " +
            "of its published contract.")]
        public async Task SystemToolContractTestAsync(string toolName, string expectedSignature)
        {
            // Arrange: host the production registration; the expected signatures mirror the retired JSON definitions.
            await using var server = await TestMcpServer.NewAsync();
            using var client = server.NewClient();

            // Act: list the tools and select the SDK-generated input schema of the tool under test.
            var catalog = await TestMcpServer.InvokeAsync(client, method: "tools/list", parameters: new { });
            var tool = catalog["result"]["tools"]
                .AsArray()
                .Single(predicate: item => item["name"].GetValue<string>() == toolName);
            var signature = string.Join(separator: ",", values: NewSignature(schema: tool["inputSchema"], prefix: ""));

            // Assert: every property path and its required marker match the published contract, nested levels included.
            Assert.AreEqual(expected: expectedSignature, actual: signature);
        }

        [TestMethod(DisplayName = "Verify system tools map expected repository failures and missing arguments " +
            "to tool errors.")]
        public async Task SystemToolFailureTestAsync()
        {
            // Arrange: host the production registration whose fake catalog rejects the reserved session.
            await using var server = await TestMcpServer.NewAsync();
            using var client = server.NewClient();
            var arguments = new { sessionId = FakeToolsRepository.MissingSessionId };

            // Act: remove a session the repository cannot find, then omit a required argument.
            var missing = await TestMcpServer.InvokeToolAsync(client, name: "g4.RemoveSession", arguments);
            var incomplete = await TestMcpServer.InvokeToolAsync(client, name: "g4.GetBuffer", arguments: new { });

            // Assert: the repository failure is a tool error whose message reaches the caller.
            Assert.IsTrue(condition: missing["result"]["isError"].GetValue<bool>());
            var failureText = missing["result"]["content"][0]["text"].GetValue<string>();
            Assert.Contains(substring: FakeToolsRepository.MissingSessionId, value: failureText);

            // Assert: a missing required argument fails before the repository is reached.
            Assert.IsTrue(condition: incomplete["result"]["isError"].GetValue<bool>());
            Assert.HasCount(expected: 1, collection: server.Catalog.Calls);
        }

        [TestMethod(DisplayName = "Verify system tools forward the published envelope " +
            "without inventing omitted optional arguments.")]
        public async Task SystemToolForwardingTestAsync()
        {
            // Arrange: host the production registration with the recording fake catalog.
            await using var server = await TestMcpServer.NewAsync();
            using var client = server.NewClient();
            var intent = new { agentIntent = "find login tools", userIntent = "log in" };
            var capabilities = new Dictionary<string, object> { ["browserName"] = "chrome" };
            var startArguments = new { driver = "ChromeDriver", driverBinaries = ".", token = "token", capabilities };
            var constraints = new { mustBeVisible = true };
            var locatorArguments = new
            {
                driverSession = "s-1",
                intent,
                openaiApiKey = "key",
                openaiModel = "model",
                openaiUri = "http://localhost",
                token = "token",
                constraints
            };
            var bufferArguments = new { sessionId = "s-1" };

            // Act: call one tool per argument family, leaving every optional argument but one unset.
            var buffer = await TestMcpServer.InvokeToolAsync(client, name: "g4.GetBuffer", arguments: bufferArguments);
            await TestMcpServer.InvokeToolAsync(client, name: "g4.FindTools", arguments: new { intent });
            await TestMcpServer.InvokeToolAsync(client, name: "g4.StartSession", arguments: startArguments);
            await TestMcpServer.InvokeToolAsync(client, name: "g4.ResolveLocator", arguments: locatorArguments);
            var bufferCall = ReadArguments(server, toolName: "g4.GetBuffer");
            var findCall = ReadArguments(server, toolName: "g4.FindTools");
            var startCall = ReadArguments(server, toolName: "g4.StartSession");
            var locatorCall = ReadArguments(server, toolName: "g4.ResolveLocator");

            // Assert: the result is the repository value as identical JSON text and structured content.
            var content = TestMcpServer.ReadContent(response: buffer);
            Assert.AreEqual(expected: "g4.GetBuffer", actual: content["tool"].GetValue<string>());
            Assert.IsTrue(condition: JsonNode.DeepEquals(node1: content, node2: buffer["result"]["structuredContent"]));

            // Assert: each envelope carries the published argument names and only the values the caller sent.
            Assert.AreEqual(expected: "sessionId", actual: ReadNames(element: bufferCall));
            Assert.AreEqual(expected: "intent", actual: ReadNames(element: findCall));
            var startNames = ReadNames(element: startCall);
            var locatorNames = ReadNames(element: locatorCall);
            Assert.AreEqual(expected: "capabilities,driver,driverBinaries,token", actual: startNames);
            var expectedLocatorNames = "constraints,driverSession,intent,openaiApiKey,openaiModel,openaiUri,token";
            Assert.AreEqual(expected: expectedLocatorNames, actual: locatorNames);

            // Assert: nested values keep their names, and unset nested options are not serialized as null.
            var agentIntent = findCall.GetProperty(propertyName: "intent").GetProperty(propertyName: "agentIntent");
            var startCapabilities = startCall.GetProperty(propertyName: "capabilities");
            var browserName = startCapabilities.GetProperty(propertyName: "browserName");
            var options = locatorCall.GetProperty(propertyName: "constraints");
            Assert.AreEqual(expected: "find login tools", actual: agentIntent.GetString());
            Assert.AreEqual(expected: "chrome", actual: browserName.GetString());
            Assert.AreEqual(expected: "mustBeVisible", actual: ReadNames(element: options));
        }

        // Flattens a JSON schema into sorted property paths, marking required members with a trailing exclamation mark.
        // Array items are walked as "name[]." so nested rule contracts are compared at the same depth as objects.
        private static List<string> NewSignature(JsonNode schema, string prefix)
        {
            // Arrange: read the declared properties and required names; dictionaries declare no properties.
            var properties = schema?["properties"]?.AsObject();
            if (properties is null)
            {
                return [];
            }
            var requiredNames = schema["required"]?.AsArray().Select(selector: item => item.GetValue<string>());
            var required = requiredNames?.ToHashSet() ?? [];

            // Act: emit each path in ordinal order and recurse into nested objects and object array items.
            var paths = new List<string>();
            var ordered = properties.OrderBy(keySelector: item => item.Key, comparer: StringComparer.Ordinal);
            foreach (var (name, value) in ordered)
            {
                var path = prefix + name;
                paths.Add(item: required.Contains(item: name) ? path + "!" : path);
                paths.AddRange(collection: NewSignature(schema: value, prefix: path + "."));
                paths.AddRange(collection: NewSignature(schema: value?["items"], prefix: path + "[]."));
            }

            return paths;
        }

        // Reads the arguments object of the single envelope the MCP layer forwarded for one tool.
        private static JsonElement ReadArguments(TestMcpServer server, string toolName)
        {
            // Arrange: select the envelope by tool name, because the fake catalog records calls in arrival order.
            var call = server
                .Catalog
                .Calls
                .Single(predicate: item => item.GetProperty(propertyName: "name").GetString() == toolName);

            return call.GetProperty(propertyName: "arguments");
        }

        // Reads the member names of one JSON object, sorted ordinally for a stable comparison.
        private static string ReadNames(JsonElement element)
        {
            // Arrange: enumerate the object members that the MCP layer built for the repository.
            var names = element
                .EnumerateObject()
                .Select(selector: item => item.Name)
                .Order(comparer: StringComparer.Ordinal);

            return string.Join(separator: ",", values: names);
        }
    }
}
