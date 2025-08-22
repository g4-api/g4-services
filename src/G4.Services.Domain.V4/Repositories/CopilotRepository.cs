using G4.Api;
using G4.Cache;
using G4.Models;
using G4.Models.Schema;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Repository for handling Copilot JSON-RPC operations:
    /// manages session state, available tools (both plugin-based and system tools),
    /// and dispatches initialization and invocation requests.
    /// </summary>
    /// <param name="clientFactory">Factory to create <see cref="HttpClient"/> instances with named configurations.</param>
    /// <param name="cache">The <see cref="CacheManager"/> instance containing plugin caches.</param>
    /// <param name="client">The <see cref="G4Client"/> used for any external service interactions.</param>
    public class CopilotRepository(IToolsRepository tools) : ICopilotRepository
    {
        #region *** Constants    ***
        // The JSON-RPC protocol version used in all responses.
        private const string JsonRpcVersion = "2.0";
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public ToolOutputSchema FindTool(string toolName, object id)
        {
            static ToolOutputSchema FindTool(
                        IToolsRepository tools,
                        string jsonrpcVersion,
                        JsonElement arguments,
                        object id)
            {
                // Extract the tool name from the arguments.
                var toolName = arguments.TryGetProperty("tool_name", out var toolNameOut)
                    ? toolNameOut.GetString()
                    : default;

                // Look up the tool by name in the internal registry.
                var tool = tools.FindTool(intent: string.Empty, toolName);

                // Initialize the response model with the provided ID and JSON-RPC version.
                var response = new ToolOutputSchema
                {
                    Id = id,
                    Jsonrpc = jsonrpcVersion
                };

                // If the tool is not found, set the error details in the response.
                if (tool == null)
                {
                    response.Error = new()
                    {
                        // JSON-RPC error code for method not found.
                        Code = -32601,

                        // Provide a message indicating the tool is missing.
                        Message = $"Tool '{toolName}' not found."
                    };
                }
                // If the tool is found, populate the result with the tool's details.
                else
                {
                    response.Result = tool;
                }

                // Return the response, either with the tool data or an error.
                return response;
            }

            // Guard against invalid input early to produce a clear failure mode.
            if (string.IsNullOrWhiteSpace(toolName))
            {
                throw new ArgumentException("Tool name must be provided.", nameof(toolName));
            }

            // Build the JSON-RPC style envelope expected by FindG4Tool
            var envelope = new Dictionary<string, object>
            {
                ["arguments"] = new Dictionary<string, object>
                {
                    ["tool_name"] = toolName
                }
            };

            // Create a JsonElement directly (no Serialize→Deserialize string round-trip).
            // Uses your configured JsonOptions (snake_case, ignore nulls, etc.).
            var arguments = JsonSerializer.SerializeToElement(envelope, ICopilotRepository.JsonOptions);

            // Delegate to the generic finder with the shared registry and protocol version.
            return FindTool(
                tools: tools,
                jsonrpcVersion: JsonRpcVersion,
                arguments,
                id);
        }

        /// <inheritdoc />
        public ToolOutputSchema GetTools(object id, params string[] types) => GetTools(id, tools, types);

        /// <inheritdoc />
        public CopilotInitializeResponseModel Initialize(object id) => new()
        {
            // Set the request ID and JSON-RPC version for the response.
            Id = id,
            Jsonrpc = JsonRpcVersion,

            // Define the result, which contains the capabilities, protocol version, and server info.
            Result = new()
            {
                Capabilities = new()
                {
                    // Indicate that the tool list has changed (can be used to trigger updates or notifications).
                    Tools = new()
                    {
                        ListChanged = true
                    }
                },

                // The protocol version the server is using (fixed value in this case).
                ProtocolVersion = "2025-03-26",

                // Define the server information (name and version).
                ServerInfo = new()
                {
                    Name = "g4-engine-copilot-mcp",
                    Version = "4.0.0"
                }
            }
        };

        /// <inheritdoc />
        public ToolOutputSchema InvokeTool(JsonElement parameters, object id)
        {
            // Match the tool by name and execute the corresponding handler.
            // Some tools are built-in system tools, others are dynamically loaded plugins.
            var result = tools.InvokeTool(parameters);

            // Construct and return the JSON-RPC response to the client.
            return new()
            {
                // Echo back the request ID so the caller can match responses to requests.
                Id = id,

                // JSON-RPC version identifier.
                Jsonrpc = JsonRpcVersion,

                // The result object contains both a text-serialized version and the structured data.
                Result = new
                {
                    // Plain text content (serialized JSON of the result object).
                    Content = new[]
                    {
                        new
                        {
                            Type = "text",
                            Text = JsonSerializer.Serialize(result, ICopilotRepository.JsonOptions)
                        }
                    },

                    // The raw structured content (original object form of the result).
                    StructuredContent = result
                }
            };
        }

        // TODO: Migrate to tools repository.
        /// <inheritdoc />
        public void SyncTools() => tools.SyncTools();

        // Retrieves the list of all available G4 tools from the internal registry and returns them in a JSON-RPC response model.
        private static ToolOutputSchema GetTools(object id, IToolsRepository tools, params string[] types)
        {
            // Filter the tools based on the specified types.
            var toolsCollection = tools.GetTools(types);

            // Return a new CopilotToolsResponseModel with the list of tools from the registry.
            return new()
            {
                // Include the request ID to correlate the response with the request.
                Id = id,
                Jsonrpc = JsonRpcVersion,
                Result = new ToolOutputSchema.ToolsResultSchema()
                {
                    // Provide the list of tools contained in the registry as the result.
                    Tools = toolsCollection.Values
                }
            };
        }
        #endregion
    }
}
