using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System.Net.Mime;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/mcp")]
    [SwaggerTag(description: "Provides the Model Context Protocol (MCP) endpoint and its tool-catalog maintenance operations. " +
        "The MCP Streamable HTTP endpoint itself is hosted by the official MCP C# SDK; this controller keeps only catalog synchronization.")]
    [ApiExplorerSettings(GroupName = "G4 Hub")]
    public class McpController(IDomain domain) : ControllerBase
    {
        #region *** Fields       ***
        // The domain service whose tools repository owns the cached G4 tool catalog served over MCP.
        private readonly IDomain _domain = domain;
        #endregion

        #region *** Methods      ***
        [HttpGet, Route("sync")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            Summary = "Sync the MCP tool catalog",
            Description = "Refreshes the cached tool definitions so that connected MCP agents have the most up-to-date list of tools.",
            Tags = ["Mcp"]
        )]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "The tools list was successfully synced. No content is returned.")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError,
            description: "An error occurred while syncing the tools list.",
            type: typeof(object),
            contentTypes: MediaTypeNames.Application.Json
        )]
        #endregion
        public IActionResult SyncTools()
        {
            // Rebuild the catalog from the plugin cache so the next MCP tools/list and tools/call observe current tools.
            _domain.G4.Tools.SyncTools();

            // Acknowledge the completed rebuild without a body, because clients re-list tools to read the new catalog.
            return NoContent();
        }
        #endregion
    }
}
