using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System.Net.Mime;

namespace G4.Services.Hub.Api.V4.Controllers
{
    /// <summary>
    /// Provides API endpoints to monitor and query connected bots.
    /// </summary>
    [ApiController]
    [Route("/api/v4/g4/[controller]")]
    [SwaggerTag(description: "Provides access to information about currently connected bots.")]
    public class BotsController(IDomain domain) : ControllerBase
    {
        private readonly IDomain _domain = domain;

        /// <summary>
        /// Returns the current status of all connected bots.
        /// </summary>
        /// <returns>A list of bot metadata objects representing each connected bot.</returns>
        [HttpGet]
        [Route("status")]
        [SwaggerOperation(
            summary: "Get the status of all connected bots",
            description: "Retrieves a list of bots currently connected to the system, including their metadata and connection status.",
            Tags = ["Bots"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "A list of all currently connected bots with their runtime details.",
            type: typeof(G4Domain.ConnectedBotModel[]),
            contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult GetStatus()
        {
            return Ok(_domain.ConnectedBots.Values);
        }
    }
}
