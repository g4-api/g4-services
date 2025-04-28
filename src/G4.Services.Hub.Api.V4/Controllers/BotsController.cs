using G4.Models;
using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

using Swashbuckle.AspNetCore.Annotations;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;

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

        [HttpDelete]
        [Route("disconnect/all")]
        [SwaggerOperation(
            summary: "Stop monitors for all bots",
            description: "Broadcasts a StopBot command to every connected client.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "StopBot commands successfully dispatched to all connected bots.")]
        public IActionResult Disconnect()
        {
            // Broadcast the StopBot signal to all connected clients
            _domain.BotsHubContext.Clients.All.SendAsync("StopBot");

            // Return 204 No Content to indicate the broadcast was sent
            return NoContent();
        }

        [HttpDelete]
        [Route("disconnect")]
        [SwaggerOperation(
            summary: "Stop monitors for multiple bots",
            description: "Sends a StopBot command to each client identified by the provided connection IDs array.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "StopBot commands successfully dispatched to the specified bots.")]
        public IActionResult Disconnect(
            [SwaggerParameter(description: "An array of SignalR connection IDs for which to stop monitoring.")][FromBody] string[] connections)
        {
            // Iterate over each connection ID and send the StopBot signal
            foreach (var connection in connections)
            {
                _domain.BotsHubContext.Clients.Client(connection).SendAsync("StopBot");
            }

            // Return 204 No Content once all commands have been sent
            return NoContent();
        }

        [HttpDelete]
        [Route("disconnect/{connection}")]
        [SwaggerOperation(
            summary: "Stop monitor for a specific bot",
            description: "Sends a StopBot command to the connected client identified by the given connection ID.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "StopBot command successfully dispatched to the specified bot.")]
        public IActionResult Disconnect(
            [SwaggerParameter(description: "The SignalR connection ID of the bot monitor instance to stop.")][FromRoute, Required] string connection)
        {
            // Send the StopBot signal to the client with this connection ID
            _domain.BotsHubContext.Clients.Client(connection).SendAsync("StopBot");

            // Return 204 No Content to indicate the command was sent
            return NoContent();
        }

        [HttpGet]
        [Route("status")]
        [SwaggerOperation(
            summary: "Get the status of all connected bots",
            description: "Retrieves a list of bots currently connected to the system, including their metadata and connection status.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status200OK, description: "A list of all currently connected bots with their runtime details.", type: typeof(ConnectedBotModel[]), contentTypes: [MediaTypeNames.Application.Json])]
        public IActionResult GetStatus()
        {
            // Retrieve and return status for every connected bot
            return Ok(_domain.Bots.GetStatus());
        }

        [HttpGet]
        [Route("status/{id}")]
        [SwaggerOperation(
            summary: "Get the status of a specific bot",
            description: "Retrieves runtime details for a single bot identified by the given Id.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status200OK, description: "The ConnectedBotModel instance for the requested Id.", type: typeof(ConnectedBotModel), contentTypes: [MediaTypeNames.Application.Json])]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "No bot found with the provided Id.", type: typeof(GenericErrorModel))]
        public IActionResult GetStatus(
            [SwaggerParameter(description: "The unique identifier of the bot to retrieve.", Required = true)] string id)
        {
            // Attempt to retrieve the specified bot
            var connectedBot = _domain.Bots.GetStatus(id);

            if (connectedBot == null)
            {
                // Return 404 if no bot exists with the given Id
                var error404 = new GenericErrorModel(HttpContext)
                    .AddError("BotNotFound", $"Bot with ID '{id}' not found.");
                return NotFound(error404);
            }

            // Return the found bot with HTTP 200
            return Ok(connectedBot);
        }

        [HttpPost]
        [Route("status")]
        [SwaggerOperation(
            summary: "Get status for multiple bots",
            description: "Retrieves runtime details for each bot specified in the request body.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status200OK, description: "An array of ConnectedBotModel instances for each valid Id.", type: typeof(ConnectedBotModel[]), contentTypes: [MediaTypeNames.Application.Json])]
        public IActionResult GetStatus(
                [SwaggerParameter(description: "Array of bot identifiers to retrieve status for.", Required = true)][FromBody] string[] ids)
        {
            // Get the status for each bot in the provided list of IDs
            var results = _domain.Bots.GetStatus(ids);

            // Return the results as an array of ConnectedBotModel instances
            return Ok(results);
        }

        [HttpPost]
        [Route("register")]
        [SwaggerOperation(
            summary: "Register a new bot",
            description: "Adds a new bot to the domain. If no ID is provided, one will be generated. Returns the registered bot model.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Bot successfully registered.", type: typeof(ConnectedBotModel), contentTypes: [MediaTypeNames.Application.Json])]
        [SwaggerResponse(StatusCodes.Status400BadRequest, description: "Invalid registration payload.", type: typeof(GenericErrorModel))]
        public IActionResult Register(
            [FromBody]
            [SwaggerParameter(description: "The registration request containing optional Id, Name, Type, and Machine values.")]
            ConnectedBotModel botModel)
        {
            // Register the bot in the domain and generate a unique ID if not provided
            var connectedBot = _domain.Bots.Register(botModel);

            // Return the fully populated model back to the caller
            return Ok(connectedBot);
        }

        [HttpDelete]
        [Route("register/{id}")]
        [SwaggerOperation(
            summary: "Unregister a single bot",
            description: "Unregisters the bot with the specified ID; on success updates its status to 'Removed' and returns the bot model.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Bot successfully unregistered; returns the updated bot model.", type: typeof(ConnectedBotModel), contentTypes: [MediaTypeNames.Application.Json])]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "No bot found with the provided ID.", type: typeof(GenericErrorModel), contentTypes: [MediaTypeNames.Application.Json])]
        [SwaggerResponse(StatusCodes.Status409Conflict, description: "Bot has an active SignalR connection and cannot be unregistered until it disconnects.", type: typeof(GenericErrorModel), contentTypes: [MediaTypeNames.Application.Json])]
        [SwaggerResponse(StatusCodes.Status502BadGateway, description: "Bot unreachable at callback URI; entry has been removed from memory and database.", type: typeof(GenericErrorModel), contentTypes: [MediaTypeNames.Application.Json])]
        public async Task<IActionResult> Unregister(
            [SwaggerParameter(description: "Unique identifier of the bot to unregister.")][FromRoute] string id)
        {
            // Attempt to unregister the bot; returns status code and the bot model (if found)
            var (statusCode, bot) = await _domain.Bots.Unregister(id);

            // 404: Bot not found in domain
            // If the bot does not exist, return 404 Not Found with error details
            if (statusCode == StatusCodes.Status404NotFound)
            {
                var error404 = new GenericErrorModel(HttpContext)
                    .AddError("BotNotFound", $"Bot with ID '{id}' not found.");
                return NotFound(error404);
            }

            // 409: Bot has an active SignalR connection and cannot be unregistered yet
            if (statusCode == StatusCodes.Status409Conflict)
            {
                var error409 = new GenericErrorModel(HttpContext)
                    .AddError("ActiveSignalRConnection", "Cannot unregister bot while its SignalR connection is active; disconnect first.");
                return Conflict(error409);
            }

            // 502: Server could not reach the bot at its callback URI (bot may be down).
            // Bot entry has been removed from both in-memory cache and database.
            if (statusCode == StatusCodes.Status502BadGateway)
            {
                var error502 = new GenericErrorModel(HttpContext)
                    .AddError("BotUnreachable", "Bot callback endpoint is unreachable; bot entry removed from memory and database.");
                return new JsonResult(error502)
                {
                    StatusCode = statusCode
                };
            }

            // Successful unregistration: update the model's status
            bot.Status = "Removed";

            // 200: Return the updated bot model
            return Ok(bot);
        }

        [HttpDelete]
        [Route("register")]
        [SwaggerOperation(
            summary: "Batch unregister bots",
            description: "Attempts to unregister each bot in the provided list of IDs. Returns the updated status for each bot—successfully removed, skipped due to active connection, or unreachable.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Returns an array of bot models with their post-unregistration status.", type: typeof(ConnectedBotModel[]), contentTypes: [MediaTypeNames.Application.Json])]
        [SwaggerResponse(StatusCodes.Status400BadRequest, description: "Request body was invalid (e.g., missing or malformed list of IDs).")]
        public async Task<IActionResult> Unregister(
            [SwaggerParameter(description: "An array of bot IDs to attempt unregistration for.")][FromBody] string[] ids)
        {
            // Invoke the domain service to unregister each bot and capture the result codes
            var unregisterResults = await _domain.Bots.Unregister(ids);

            // Return 200 OK with the list of bots and their final statuses
            return Unregister(unregisterResults);
        }

        [HttpDelete]
        [Route("register/all")]
        [SwaggerOperation(
            summary: "Batch unregister all disconnected bots",
            description: "Attempts to unregister every bot that has no active SignalR connection; returns each bot's final status.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status200OK, description: "An array of bot models with updated post-unregistration statuses.", type: typeof(ConnectedBotModel[]), contentTypes: [MediaTypeNames.Application.Json])]
        public async Task<IActionResult> Unregister()
        {
            // Invoke the domain service to unregister all eligible bots and get back (statusCode, bot) tuples
            var unregisterResults = await _domain.Bots.Unregister();

            // Delegate to the existing batch Unregister overload to produce the HTTP 200 response
            return Unregister(unregisterResults);
        }

        // Unregister method for batch processing of multiple bots
        private static JsonResult Unregister(IEnumerable<(int StatusCode, ConnectedBotModel ConnectedBot)> unregisterResults)
        {
            // Initialize a list to hold the unregistered bots
            var unregisteredBots = new List<ConnectedBotModel>();

            // For each tuple of (HTTP status, bot model), set the status property accordingly
            foreach (var (statusCode, bot) in unregisterResults)
            {
                // Set the status property based on the HTTP status code
                bot.Status = statusCode switch
                {
                    StatusCodes.Status200OK => "Removed",
                    StatusCodes.Status409Conflict => "Conflict",
                    StatusCodes.Status502BadGateway => "Unreachable",
                    _ => "Unknown"
                };

                // Add the bot to the list of unregistered bots
                unregisteredBots.Add(bot);
            }

            // Return 200 OK with the list of bots and their final statuses
            return new JsonResult(unregisteredBots)
            {
                StatusCode = StatusCodes.Status200OK
            };
        }

        [HttpPut]
        [Route("register/{id}")]
        [SwaggerOperation(
            summary: "Update bot metadata and status",
            description: "Applies the provided metadata and status to the bot identified by the given ID. Returns the updated bot model.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Bot successfully updated; returns the updated bot model.", type: typeof(ConnectedBotModel), contentTypes: [MediaTypeNames.Application.Json])]
        [SwaggerResponse(StatusCodes.Status400BadRequest, description: "Request payload is missing required parameters or is invalid.", type: typeof(GenericErrorModel), contentTypes: [MediaTypeNames.Application.Json])]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "No bot found with the provided ID.", type: typeof(GenericErrorModel), contentTypes: [MediaTypeNames.Application.Json])]
        public IActionResult Update(
            [SwaggerParameter(description: "Unique identifier of the bot to update.")][FromRoute] string id,
            [SwaggerParameter(description: "Updated bot metadata payload.")][FromBody] ConnectedBotModel botModel)
        {
            // Delegate the update operation to the domain service, capturing status and bot instance
            var (statusCode, bot) = _domain.Bots.Update(id, botModel);

            // If the bot does not exist, return 404 Not Found with an error payload
            if (statusCode == StatusCodes.Status404NotFound)
            {
                var error404 = new GenericErrorModel(HttpContext)
                    .AddError("BotNotFound", $"Bot with ID '{id}' not found.");
                return NotFound(error404);
            }

            // Apply the new status and update the modification timestamp
            bot.Status = botModel.Status;
            bot.LastModifiedOn = DateTime.UtcNow;

            // Return 200 OK with the updated bot model
            return Ok(bot);
        }

        [HttpGet]
        [Route("test/all")]
        [SwaggerOperation(
            summary: "Test connectivity for all bots",
            description: "Checks connectivity for every registered bot and returns the updated bot models with their current status.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status200OK, description: "A list of all bots with their latest connectivity status.", type: typeof(ConnectedBotModel[]), contentTypes: [MediaTypeNames.Application.Json])]
        public async Task<IActionResult> TestConnection()
        {
            // Call the domain service to perform connectivity checks on all bots
            var bots = await _domain.Bots.TestConnection();

            // Select and return only the ConnectedBotModel from each result
            return Ok(bots.Select(result => result.ConnectedBot));
        }

        [HttpPost]
        [Route("test")]
        [SwaggerOperation(
            summary: "Test connectivity for specific bots",
            description: "Checks the connection status of each bot with the given IDs and returns their updated models.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status200OK, description: "A list of bot models with their latest connectivity status.", type: typeof(ConnectedBotModel[]), contentTypes: [MediaTypeNames.Application.Json])]
        public async Task<IActionResult> TestConnection(
            [SwaggerParameter(description: "An array of unique bot identifiers to test connectivity for.")][FromBody, Required] string[] ids)
        {
            // Invoke the domain service to perform connectivity checks for the specified bot IDs
            var bots = await _domain.Bots.TestConnection(ids);

            // Extract and return only the ConnectedBotModel from each result tuple
            return Ok(bots.Select(result => result.ConnectedBot));
        }

        [HttpGet]
        [Route("test/{id}")]
        [SwaggerOperation(
            summary: "Test connectivity for a specific bot",
            description: "Checks if the bot has an active SignalR connection or responds at its callback endpoint; updates and returns its model.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Bot model returned with updated status (online or offline).", type: typeof(ConnectedBotModel), contentTypes: [MediaTypeNames.Application.Json])]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "No bot found with the provided ID.", type: typeof(GenericErrorModel), contentTypes: [MediaTypeNames.Application.Json])]
        public async Task<IActionResult> TestConnection(
            [SwaggerParameter(description: "The unique bot ID to test connectivity for.")][FromRoute, Required] string id)
        {
            // Call into the domain service to perform the connectivity check
            var (statusCode, bot) = await _domain.Bots.TestConnection(id);

            // If the bot does not exist, return 404 with an error payload
            if (statusCode == StatusCodes.Status404NotFound)
            {
                var error404 = new GenericErrorModel(HttpContext)
                    .AddError("BotNotFound", $"Bot with ID '{id}' not found.");
                return NotFound(error404);
            }

            // Map the returned status code to the appropriate IActionResult,
            // always including the bot model in the response body
            return Ok(bot);
        }
    }
}
