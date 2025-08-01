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
        /// Invokes a previously generated tool with the provided parameters.
        /// </summary>
        /// <param name="parameters">A <see cref="JsonElement"/> containing the parameters as definedby the tool's input schema.</param>
        /// <param name="id">The JSON-RPC request identifier to correlate response.</param>
        /// <returns>An object representing the raw JSON-RPC response payload, includingany result or error information from the tool.</returns>
        object InvokeTool(JsonElement parameters, object id);
    }
}
