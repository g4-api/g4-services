using System.Collections.Generic;

namespace G4.Models
{
    /// <summary>
    /// Represents the external plugin sources used during cache synchronization.
    /// </summary>
    public class CacheSyncModel
    {
        /// <summary>
        /// Gets or sets the external repositories that should be synchronized into the cache.
        /// </summary>
        public G4ExternalRepositoryModel[] Repositories { get; set; }

        /// <summary>
        /// Gets or sets the Model Context Protocol (MCP) servers that should be synchronized into the cache.
        /// </summary>
        public Dictionary<string, McpServerModel> Servers { get; set; }
    }
}
