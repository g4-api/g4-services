using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

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

        [HttpGet]
        [Route("status")]
        [SwaggerOperation(
            summary: "Get the status of all connected bots",
            description: "Retrieves a list of bots currently connected to the system, including their metadata and connection status.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "A list of all currently connected bots with their runtime details.",
            type: typeof(G4Domain.ConnectedBotModel[]),
            contentTypes: [MediaTypeNames.Application.Json])]
        public IActionResult GetStatus()
        {
            // Return 200 OK with the collection of connected bots
            return Ok(_domain.ConnectedBots.Values);
        }

        [HttpDelete]
        [Route("stop/{connection}")]
        [SwaggerOperation(
            summary: "Stop monitor for a specific bot",
            description: "Sends a StopMonitor command to the connected client identified by the given connection ID.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "StopMonitor command successfully dispatched to the specified bot.")]
        public IActionResult Stop(
            [SwaggerParameter(description: "The SignalR connection ID of the bot monitor instance to stop.")] string connection)
        {
            // Send the StopMonitor signal to the client with this connection ID
            _domain.BotsHubContext.Clients.Client(connection).SendAsync("StopMonitor");

            // Return 204 No Content to indicate the command was sent
            return NoContent();
        }

        [HttpDelete]
        [Route("stop")]
        [SwaggerOperation(
            summary: "Stop monitors for multiple bots",
            description: "Sends a StopMonitor command to each client identified by the provided connection IDs array.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "StopMonitor commands successfully dispatched to the specified bots.")]
        public IActionResult Stop(
            [SwaggerParameter(description: "An array of SignalR connection IDs for which to stop monitoring.")][FromBody] string[] connections)
        {
            // Iterate over each connection ID and send the StopMonitor signal
            foreach (var connection in connections)
            {
                _domain.BotsHubContext.Clients.Client(connection).SendAsync("StopMonitor");
            }

            // Return 204 No Content once all commands have been sent
            return NoContent();
        }

        [HttpDelete]
        [Route("stop/all")]
        [SwaggerOperation(
            summary: "Stop monitors for all bots",
            description: "Broadcasts a StopMonitor command to every connected client.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "StopMonitor commands successfully dispatched to all connected bots.")]
        public IActionResult Stop()
        {
            // Broadcast the StopMonitor signal to all connected clients
            _domain.BotsHubContext.Clients.All
                .SendAsync("StopMonitor");

            // Return 204 No Content to indicate the broadcast was sent
            return NoContent();
        }
    }
}
