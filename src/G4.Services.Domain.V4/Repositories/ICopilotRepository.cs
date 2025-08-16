using G4.Models;

using System.Text.Json;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Defines methods for persisting and retrieving Copilot JSON-RPC operations,
    /// including tool discovery, initialization, and invocation.
    /// </summary>
    public interface ICopilotRepository
    {
        /// <summary>
        /// Retrieves the metadata and schema for a single tool by its unique name.
        /// </summary>
        /// <param name="toolName">The unique identifier of the tool to find.</param>
        /// <param name="id">The JSON-RPC request identifier to correlate response.</param>
        /// <returns>A <see cref="CopilotToolsResponseModel"/> containing the tool's schema,description, and any additional metadata.</returns>
        CopilotToolsResponseModel FindTool(string toolName, object id);

        /// <summary>
        /// Retrieves the full list of available tools that the Copilot agent can invoke.
        /// </summary>
        /// <param name="id">The JSON-RPC request identifier to correlate response.</param>
        /// <returns>A <see cref="CopilotToolsResponseModel"/> wrapping the collection of tools.</returns>
        CopilotToolsResponseModel GetTools(object id);

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
        /// <returns>A <see cref="CopilotToolsResponseModel"/> containing the result of the tool execution.</returns>
        CopilotToolsResponseModel InvokeTool(JsonElement parameters, object id);

        /// <summary>
        /// Sync the list of tools available to the Copilot agent by refreshing 
        /// the underlying cache data.
        /// </summary>
        void SyncTools();
    }
}
