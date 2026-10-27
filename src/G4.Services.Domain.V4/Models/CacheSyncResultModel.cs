using G4.Api.Models;

using System.Collections.Generic;

namespace G4.Models
{
    /// <summary>
    /// Represents the outcome of a plugin cache synchronization, returned by <c>POST cache/sync</c>.
    /// </summary>
    /// <remarks>
    /// The Hub returns the per-server results so a client can report an MCP server that was not synchronized, or
    /// plugins the engine rejected, instead of finding them only in the Hub log. The results are wrapped in an object,
    /// not returned as a bare list, so repository results can be added later without breaking the response shape;
    /// repositories are not reported today because the engine client's repository synchronization returns no result.
    /// </remarks>
    public class CacheSyncResultModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or initializes the synchronization result of every MCP server in the request, in the order the engine
        /// client reported them.
        /// </summary>
        /// <remarks>
        /// Empty when the request named no MCP servers.
        /// </remarks>
        public IReadOnlyList<McpServerSyncResultModel> Servers { get; init; } = [];
        #endregion
    }
}
