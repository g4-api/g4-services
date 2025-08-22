using G4.Models;

using System.Collections.Generic;
using System.Text.Json;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Defines a contract for managing and interacting with MCP tools,
    /// including discovery, retrieval, invocation, and synchronization.
    /// </summary>
    public interface IToolsRepository
    {
        /// <summary>
        /// Finds a specific tool by its unique name.
        /// </summary>
        /// <param name="intent">The intent or purpose for which the tool is being sought.</param>
        /// <param name="toolName">The unique identifier or name of the tool to locate.</param>
        /// <returns>A <see cref="McpToolModel"/> representing the tool if found; otherwise, <c>null</c>.</returns>
        McpToolModel FindTool(string intent, string toolName);

        /// <summary>
        /// Retrieves a collection of available tools, optionally filtered by type(s).
        /// </summary>
        /// <param name="types">Optional tool type filters. If not provided, all available tools are returned.</param>
        /// <returns>A dictionary mapping tool names (<see cref="string"/>) to their corresponding <see cref="McpToolModel"/> definitions.</returns>
        IDictionary<string, McpToolModel> GetTools(params string[] types);

        /// <summary>
        /// Invokes a tool dynamically using its input parameters.
        /// </summary>
        /// <param name="parameters">A <see cref="JsonElement"/> containing the tool invocation parameters (e.g., arguments, configuration, or input data).</param>
        /// <returns>The result of the tool execution, as an <see cref="object"/>.</returns>
        object InvokeTool(JsonElement parameters);

        /// <summary>
        /// Synchronizes the tool repository with its source (e.g., database, file system, or registry),
        /// ensuring the repository reflects the latest tool definitions.
        /// </summary>
        void SyncTools();
    }
}
