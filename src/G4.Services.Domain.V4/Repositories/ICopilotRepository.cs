using G4.Converters;
using G4.Models;
using G4.Models.Schema;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Defines methods for persisting and retrieving Copilot JSON-RPC operations,
    /// including tool discovery, initialization, and invocation.
    /// </summary>
    public interface ICopilotRepository
    {
        /// <summary>
        /// Ges the JSON serialization options used for Copilot operations.
        /// </summary>
        public static JsonSerializerOptions JsonOptions
        {
            get
            {
                // Create a fresh options instance.
                // If this is on a hot path, consider caching in a static readonly field to avoid per-call allocations.
                var options = new JsonSerializerOptions
                {
                    // Do not write properties whose value is null.
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

                    // Serialize dictionary keys in snake_case (e.g., "error_code").
                    DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,

                    // Serialize CLR property names in snake_case (e.g., "request_id").
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,

                    // Compact output optimized for transport/log size. Set true for dev readability.
                    WriteIndented = false
                };

                // Serialize System.Type in a stable, readable format.
                options.Converters.Add(new TypeConverter());

                // Normalize exception payloads (type, message, stack trace, etc.).
                options.Converters.Add(new ExceptionConverter());

                // Enforce ISO-8601 DateTime text to avoid locale/round-trip issues.
                options.Converters.Add(new DateTimeIso8601Converter());

                // Provide a readable/portable representation for MethodBase (useful in logs/telemetry).
                options.Converters.Add(new MethodBaseConverter());

                // Return the configured options.
                return options;
            }
        }

        /// <summary>
        /// Gets the JSON serialization options used for G4 protocol operations.
        /// </summary>
        public static JsonSerializerOptions G4JsonOptions
        {
            get
            {
                // Create a fresh options instance.
                // If this is on a hot path, consider caching in a static readonly field to avoid per-call allocations.
                var options = new JsonSerializerOptions
                {
                    // Do not write properties whose value is null.
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

                    // Serialize dictionary keys in snake_case (e.g., "error_code").
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,

                    // Serialize CLR property names in snake_case (e.g., "request_id").
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

                    // Compact output optimized for transport/log size. Set true for dev readability.
                    WriteIndented = false
                };

                // Serialize System.Type in a stable, readable format.
                options.Converters.Add(new TypeConverter());

                // Normalize exception payloads (type, message, stack trace, etc.).
                options.Converters.Add(new ExceptionConverter());

                // Enforce ISO-8601 DateTime text to avoid locale/round-trip issues.
                options.Converters.Add(new DateTimeIso8601Converter());

                // Provide a readable/portable representation for MethodBase (useful in logs/telemetry).
                options.Converters.Add(new MethodBaseConverter());

                // Return the configured options.
                return options;
            }
        }

        /// <summary>
        /// Retrieves the metadata and schema for a single tool by its unique name.
        /// </summary>
        /// <param name="toolName">The unique identifier of the tool to find.</param>
        /// <param name="id">The JSON-RPC request identifier to correlate response.</param>
        /// <returns>A <see cref="ToolResponseModel"/> containing the tool's schema,description, and any additional metadata.</returns>
        ToolOutputSchema FindTool(string toolName, object id);

        /// <summary>
        /// Retrieves the full list of available tools that the Copilot agent can invoke.
        /// </summary>
        /// <param name="id">The JSON-RPC request identifier to correlate response.</param>
        /// <param name="types">An optional list of tool type filters. If provided, only tools matching the specified types will be returned; otherwise, all available tools are included.
        /// </param>
        /// <returns>A <see cref="ToolResponseModel"/> wrapping the collection of tools.</returns>
        ToolOutputSchema GetTools(object id, params string[] types);

        /// <summary>
        /// Handles the "initialize" JSON-RPC method, returning protocol capabilities
        /// and server information.
        /// </summary>
        /// <param name="id">The JSON-RPC request identifier to correlate response.</param>
        /// <returns>A <see cref="CopilotInitializeResponseModel"/> containing protocol version,supported features, and server details.</returns>
        CopilotInitializeResponseModel Initialize(object id);

        /// <summary>
        /// Invokes the specified tool with the provided parameters and returns a JSON-RPC response containing the tool's result.
        /// This method handles both system tools (built-in) and plugin-based tools (via action rules).
        /// </summary>
        /// <param name="parameters">The JSON parameters for invoking the tool, including tool name and arguments.</param>
        /// <param name="id">The request ID to correlate the response with the request.</param>
        /// <returns>A <see cref="ToolResponseModel"/> containing the result of the tool execution.</returns>
        ToolOutputSchema InvokeTool(JsonElement parameters, object id);

        /// <summary>
        /// Sync the list of tools available to the Copilot agent by refreshing 
        /// the underlying cache data.
        /// </summary>
        void SyncTools();
    }
}
