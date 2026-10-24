using G4.Settings;

using ModelContextProtocol.Protocol;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Mcp
{
    // Converts domain results and expected domain failures into MCP tool results for every G4 tool family.
    // The attribute-declared system and recording tools and the plugin call handler all route through this helper,
    // so MCP clients observe one result shape: JSON text, plus structured content whenever the value is an object.
    internal static class McpResults
    {
        #region *** Methods      ***

        // Runs one domain operation and encodes its value, mapping expected failures to MCP tool errors.
        // Unexpected exceptions stay with the SDK, which logs them without exposing internal details to the caller.
        internal static async Task<CallToolResult> InvokeAsync(Func<Task<object>> operation)
        {
            try
            {
                // Complete the shared domain operation before constructing a transport-specific result.
                var value = await operation().ConfigureAwait(false);

                // Encode the completed value so text-only and structured MCP consumers receive the same data.
                return NewResult(value);
            }
            catch (Exception exception) when (TestExpectedFailure(exception))
            {
                // Tool execution failures belong in MCP content rather than in a JSON-RPC protocol error.
                return NewErrorResult(message: exception.Message);
            }
        }

        // Builds a tool error whose message is visible to the model so it can correct the call and retry.
        internal static CallToolResult NewErrorResult(string message) => new()
        {
            IsError = true,
            Content = [new TextContentBlock { Text = message }]
        };

        // Serializes with the application JSON contract so MCP payloads match the REST representation exactly.
        // A null value becomes the JSON text "null" without structured content, which marks an empty result.
        internal static CallToolResult NewResult(object value)
        {
            // Serialize once into an element so the text and structured forms can never diverge.
            var element = JsonSerializer.SerializeToElement(value, options: AppSettings.JsonOptions);
            var result = new CallToolResult
            {
                Content = [new TextContentBlock { Text = element.GetRawText() }]
            };

            // Attach structured content only for objects, because the MCP contract defines it as a JSON object.
            if (element.ValueKind == JsonValueKind.Object)
            {
                result.StructuredContent = element;
            }

            return result;
        }

        // Identifies the domain failures whose messages are safe and actionable for an MCP caller.
        // These are the same exception families the REST pipeline maps to 400, 404, 409, and 502 responses.
        private static bool TestExpectedFailure(Exception exception) => exception is ArgumentException
            or JsonException
            or KeyNotFoundException
            or InvalidOperationException
            or HttpRequestException;

        #endregion
    }
}
