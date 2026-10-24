using G4.Attributes;
using G4.Models;
using G4.Models.Schema;
using G4.Services.Domain.V4.Extensions;
using G4.Services.Domain.V4.Models;
using G4.Services.Domain.V4.Models.Schema;
using G4.Services.Domain.V4.Repositories;
using G4.Services.UnitTests.Mcp;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Services.UnitTests.Recordings
{
    // Hosts the production MCP registration (AddG4Mcp and MapG4Mcp) on a real loopback socket.
    // The host owns real recordings and flows repositories, a fake tool catalog, and an in-memory flows client, so
    // MCP tests exercise the SDK wire protocol and every tool family without booting the full Hub, its cache, or LiteDB.
    internal sealed class TestMcpServer(WebApplication application) : IAsyncDisposable
    {
        #region *** Constants    ***

        // Negotiated protocol version sent by every request, matching what a current MCP client announces.
        internal const string ProtocolVersion = "2025-11-25";

        // String request identifier echoed by the SDK, so each helper call can verify response correlation.
        private const string RequestId = "test-request";

        #endregion

        #region *** Fields       ***

        // Retains the app so each test releases its listener and the repository's recorder connections.
        private readonly WebApplication _application = application;

        #endregion

        #region *** Properties   ***

        // Exposes the fake catalog so tests can assert the envelopes the system tools forward to the repository.
        internal FakeToolsRepository Catalog => _application.Services.GetRequiredService<FakeToolsRepository>();

        // Exposes the in-memory flows client behind the real flows repository, so flow tools can be asserted.
        internal FakeFlowsClient Flows => _application.Services.GetRequiredService<FakeFlowsClient>();

        // Exposes the shared repository so tests can prove that sessions are transport-neutral.
        internal IRecordingsRepository Recordings => _application.Services.GetRequiredService<IRecordingsRepository>();

        #endregion

        #region *** Methods      ***

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            // Cleanup: dispose the host, which also disposes the repository and stops any remaining sessions.
            await _application.DisposeAsync().ConfigureAwait(false);
        }

        // Sends a real JSON-RPC request and accepts either response encoding allowed by Streamable HTTP.
        // Fails the calling test when the HTTP status is not 200 or the response does not echo the request identifier.
        internal static async Task<JsonNode> InvokeAsync(HttpClient client, string method, object parameters)
        {
            // Arrange: own a bounded request lifetime so a stalled endpoint fails the test instead of hanging it.
            using var timeout = new CancellationTokenSource(delay: TimeSpan.FromSeconds(seconds: 15));

            // Arrange: use explicit protocol and accept headers so requests match an MCP client's wire contract.
            using var request = NewRequest(content: new { jsonrpc = "2.0", id = RequestId, method, @params = parameters });

            // Act: send the request and read the complete body, which a stateless response always finishes.
            using var response = await client.SendAsync(request, cancellationToken: timeout.Token);
            var content = await response.Content.ReadAsStringAsync(cancellationToken: timeout.Token);
            Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode, message: content);

            // Arrange: unwrap an SSE frame before applying the shared JSON-RPC assertions.
            if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
            {
                var lines = content.Split(separator: '\n');
                var dataLine = lines.First(predicate: line => line.StartsWith(value: "data: ", comparisonType: StringComparison.Ordinal));
                content = dataLine["data: ".Length..];
            }

            // Assert: the SDK preserves string request identifiers in both results and errors.
            var result = JsonNode.Parse(json: content);
            Assert.AreEqual(expected: RequestId, actual: result["id"].GetValue<string>());

            return result;
        }

        // Calls one MCP tool through tools/call and returns the complete JSON-RPC response.
        internal static Task<JsonNode> InvokeToolAsync(HttpClient client, string name, object arguments)
        {
            // Route through the generic JSON-RPC helper so tool calls share its framing and correlation checks.
            return InvokeAsync(client, method: "tools/call", parameters: new { name, arguments });
        }

        // Creates an isolated MCP host with the production registration and an OS-assigned loopback port.
        internal static async Task<TestMcpServer> NewAsync()
        {
            // Arrange: use loopback and an OS-assigned port rather than a shared hard-coded endpoint.
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.Sources.Clear();
            builder.WebHost.ConfigureKestrel(options: options => options.Listen(address: IPAddress.Loopback, port: 0));
            builder.Logging.ClearProviders();

            // Arrange: register the repositories and MCP server the Hub uses, with fakes in place of plugins and LiteDB.
            builder.Services.AddSingleton<FakeToolsRepository>();
            builder.Services.AddSingleton<IToolsRepository>(implementationFactory: provider =>
                provider.GetRequiredService<FakeToolsRepository>());
            builder.Services.AddSingleton<IRecordingsRepository, RecordingsRepository>();
            builder.Services.AddSingleton<FakeFlowsClient>();
            builder.Services.AddSingleton<IFlowsRepository>(implementationFactory: provider =>
                new FlowsRepository(flows: provider.GetRequiredService<FakeFlowsClient>()));
            builder.Services.AddG4Mcp();

            // Arrange: map the production MCP route and start before returning so requests reach a live endpoint.
            var application = builder.Build();
            application.MapG4Mcp();
            await application.StartAsync().ConfigureAwait(false);

            return new TestMcpServer(application);
        }

        // Creates a client bound to the listener address assigned by the operating system.
        internal HttpClient NewClient()
        {
            // Read the bound listener after startup so the client targets the actual port.
            var server = _application.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>().Addresses.Single();

            return new HttpClient { BaseAddress = new Uri(uriString: address) };
        }

        // Builds one Streamable HTTP POST carrying the headers every MCP client sends.
        internal static HttpRequestMessage NewRequest(object content)
        {
            // Arrange: declare both accepted encodings and the negotiated protocol version.
            var request = new HttpRequestMessage(method: HttpMethod.Post, requestUri: McpServiceExtensions.McpRoute);
            request.Headers.TryAddWithoutValidation(name: "Accept", value: "application/json, text/event-stream");
            request.Headers.Add(name: "MCP-Protocol-Version", value: ProtocolVersion);
            request.Content = JsonContent.Create(inputValue: content);

            return request;
        }

        // Decodes the JSON text of a successful tool result, including a JSON null empty flow.
        internal static JsonNode ReadContent(JsonNode response)
        {
            // Assert: fail on tool errors before parsing their human-readable diagnostics as JSON.
            var isError = response["result"]?["isError"]?.GetValue<bool>() ?? false;
            Assert.IsFalse(condition: isError, message: response.ToJsonString());

            // Arrange: preserve JSON null as the empty-recording result rather than synthesizing an object.
            return JsonNode.Parse(json: response["result"]["content"][0]["text"].GetValue<string>());
        }

        #endregion
    }

    // Records every envelope the system tools forward to the repository and echoes it back as the tool result.
    // Only CallTool is reached by the MCP layer; every other member reports that it is unused, so a test fails loudly
    // when the MCP layer starts depending on another repository member or exposes catalog tools by name again.
    internal sealed class FakeToolsRepository : IToolsRepository
    {
        #region *** Constants    ***

        // Names the session that no system tool can find, so missing-resource failures can be asserted.
        internal const string MissingSessionId = "missing-session";

        // Names a catalog (plugin) tool, which the router runs through g4.SendRule and tools/call never reaches.
        internal const string ToolName = "FakePluginTool";

        #endregion

        #region *** Properties   ***

        // Retains every call envelope so tests can assert name and argument forwarding.
        internal ConcurrentQueue<JsonElement> Calls { get; } = [];

        #endregion

        #region *** Methods      ***

        /// <inheritdoc />
        public object CallTool(JsonElement parameters)
        {
            // Record the forwarded envelope before producing a result so failures are still observable.
            Calls.Enqueue(item: parameters.Clone());
            var name = parameters.GetProperty(propertyName: "name").GetString();

            // Raise a missing-resource failure for the reserved session so system-tool errors can be asserted.
            var arguments = parameters.GetProperty(propertyName: "arguments");
            var hasSession = arguments.TryGetProperty(propertyName: "sessionId", value: out var sessionId);
            if (hasSession && sessionId.GetString() == MissingSessionId)
            {
                throw new KeyNotFoundException(message: $"Session '{MissingSessionId}' was not found.");
            }

            return new { Tool = name, Arguments = arguments };
        }

        /// <inheritdoc />
        public object FindExamples(JsonElement parameters) => throw new NotSupportedException();

        /// <inheritdoc />
        public object FindExamples(ExamplesQueryModel query) => throw new NotSupportedException();

        /// <inheritdoc />
        public McpToolModel FindTool(string intent, string toolName) => throw new NotSupportedException();

        /// <inheritdoc />
        public IDictionary<string, McpToolModel> FindTools(string prompt) => throw new NotSupportedException();

        /// <inheritdoc />
        public IDictionary<string, McpToolModel> FindTools(JsonElement parameters) => throw new NotSupportedException();

        /// <inheritdoc />
        public IDictionary<string, McpToolModel> FindTools(string prompt, int maxResults) => throw new NotSupportedException();

        /// <inheritdoc />
        public IDictionary<string, McpToolModel> FindTools(string intent, int maxResults, int threshold) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public BufferResponseModel GetBuffer(string sessionId) => throw new NotSupportedException();

        /// <inheritdoc />
        public IDictionary<string, object> GetDocumentModel(string driverSession, string token) => throw new NotSupportedException();

        /// <inheritdoc />
        public object RegisterCapability(G4PluginAttribute manifest) => throw new NotSupportedException();

        /// <inheritdoc />
        public object RemoveBuffer(string sessionId) => throw new NotSupportedException();

        /// <inheritdoc />
        public object RemoveSession(string sessionId) => throw new NotSupportedException();

        /// <inheritdoc />
        public object ResolveLocator(ResolveLocatorInputSchema schema) => throw new NotSupportedException();

        /// <inheritdoc />
        public object SendRule(SendRuleInputSchema schema) => throw new NotSupportedException();

        /// <inheritdoc />
        public object SendRules(SendRulesInputSchema schema) => throw new NotSupportedException();

        /// <inheritdoc />
        public object StartSession(StartSessionInputSchema schema) => throw new NotSupportedException();

        /// <inheritdoc />
        public void SyncTools() => throw new NotSupportedException();

        #endregion
    }
}
