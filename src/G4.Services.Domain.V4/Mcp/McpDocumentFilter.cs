using G4.Services.Domain.V4.Extensions;

using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace G4.Services.Domain.V4.Mcp
{
    /// <summary>
    /// Documents the SDK-owned MCP endpoint, which is outside MVC API discovery.
    /// </summary>
    /// <remarks>
    /// The endpoint is mapped by <see cref="McpServiceExtensions.MapG4Mcp"/> rather than by a controller, so
    /// Swashbuckle never sees it. This filter adds the stateless POST operation to the MCP and full G4 API documents
    /// only, leaving every other document unchanged. tools/list remains the authoritative tool schema catalog.
    /// </remarks>
    public sealed class McpDocumentFilter : IDocumentFilter
    {
        #region *** Constants    ***

        // Names the Swagger documents that publish the MCP endpoint: the MCP document and the full G4 API document.
        private static readonly HashSet<string> DocumentNames = new(
            collection: ["mcp", "g4-api"],
            comparer: StringComparer.OrdinalIgnoreCase);

        #endregion

        #region *** Methods      ***

        /// <inheritdoc />
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            // Skip unrelated documents so feature-scoped Swagger pages do not list the protocol endpoint.
            if (!DocumentNames.Contains(item: context.DocumentName))
            {
                return;
            }

            // Describe the JSON-RPC envelope while tools/list remains the authoritative tool schema catalog.
            var schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Required = new HashSet<string> { "jsonrpc", "method" },
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["jsonrpc"] = new OpenApiSchema { Type = JsonSchemaType.String, Default = JsonValue.Create(value: "2.0") },
                    ["id"] = new OpenApiSchema { Description = "String or integer request ID; omit for notifications." },
                    ["method"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["params"] = new OpenApiSchema { Type = JsonSchemaType.Object, AdditionalPropertiesAllowed = true }
                }
            };
            var example = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/list" };

            // Explain the supported protocol surface and tool families in one operation description.
            var description = "Stateless MCP Streamable HTTP. Supports initialize/server discovery, ping, " +
                "tools/list, and tools/call. Recording tools: g4.StartRecording (arguments.settings is the REST " +
                "recordings start body), g4.GetRecordingStatus, g4.StopRecording, and g4.GetAutomation " +
                "(arguments.id). Flow tools: g4.RegisterFlow (arguments.automation is the native automation object), " +
                "g4.GetFlow, g4.GetFlows, g4.InvokeFlow, g4.RemoveFlow, and g4.StopFlowInvocation. tools/list " +
                "advertises every g4.* tool; catalog (plugin) tools are not callable by name: find them with " +
                "g4.FindTools or g4.FindTool and run them with g4.SendRule or g4.SendRules. Set Accept to " +
                "application/json, text/event-stream and MCP-Protocol-Version to your negotiated version. No MCP " +
                "session ID is required. Recording IDs and flows are shared with REST.";

            // Document the negotiated version header so Swagger requests match the protocol a current client announces.
            var protocolVersionHeader = new OpenApiParameter
            {
                Name = "MCP-Protocol-Version",
                In = ParameterLocation.Header,
                Description = "Negotiated MCP protocol version.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Default = JsonValue.Create(value: "2025-11-25") }
            };

            // Add only the supported stateless POST transport; GET streaming and DELETE sessions are unavailable.
            swaggerDoc.Paths[McpServiceExtensions.McpRoute] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Post] = new OpenApiOperation
                    {
                        OperationId = "McpRequest",
                        Summary = "Send an MCP JSON-RPC request",
                        Description = description,
                        Tags = new HashSet<OpenApiTagReference> { new(referenceId: "Mcp", hostDocument: swaggerDoc) },
                        Parameters = [protocolVersionHeader],
                        RequestBody = new OpenApiRequestBody
                        {
                            Required = true,
                            Content = new Dictionary<string, OpenApiMediaType>
                            {
                                ["application/json"] = new() { Schema = schema, Example = example }
                            }
                        },
                        Responses = new OpenApiResponses
                        {
                            ["200"] = new OpenApiResponse
                            {
                                Description = "JSON-RPC result or error, optionally carried in SSE data frames. " +
                                    "Tool failures use result.isError.",
                                Content = new Dictionary<string, OpenApiMediaType>
                                {
                                    ["application/json"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.Object } },
                                    ["text/event-stream"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String } }
                                }
                            },
                            ["202"] = new OpenApiResponse { Description = "Notification accepted; no response body." },
                            ["400"] = new OpenApiResponse { Description = "Invalid protocol request." },
                            ["406"] = new OpenApiResponse { Description = "Unsupported Accept header." },
                            ["415"] = new OpenApiResponse { Description = "Expected application/json request content." }
                        }
                    }
                }
            };
        }

        #endregion
    }
}
