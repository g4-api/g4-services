using G4.Services.Domain.V4.Repositories;

using ModelContextProtocol.Protocol;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Mcp
{
    // Executes one G4 system tool through IToolsRepository.CallTool on behalf of the attribute-declared tools.
    // Catalog (plugin) tools have no MCP entry point of their own: clients discover them through g4.FindTools and
    // g4.FindTool and run them through g4.SendRule or g4.SendRules, so this helper is the only path into CallTool.
    internal static class G4ToolsInvoker
    {
        #region *** Fields       ***

        // Serializes rebuilt arguments with camelCase names and without absent optional values, so the repository
        // receives the same envelope a client sends and applies its own defaults for omitted arguments.
        private static readonly JsonSerializerOptions s_argumentsOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        #endregion

        #region *** Methods      ***

        // Builds the name-and-arguments envelope consumed by IToolsRepository.CallTool and executes it.
        // Absent arguments become an empty object, and expected domain failures surface as MCP tool errors.
        internal static Task<CallToolResult> InvokeAsync(IToolsRepository tools, string name, object arguments)
        {
            // Serialize once into the envelope so every system tool reaches the repository in one shape.
            var envelope = new { name, arguments = arguments ?? new { } };
            var parameters = JsonSerializer.SerializeToElement(envelope, options: s_argumentsOptions);

            // Execute through the shared helper so every G4 tool family returns the same result shape.
            return McpResults.InvokeAsync(operation: () => Task.FromResult(result: tools.CallTool(parameters)));
        }

        #endregion
    }
}
