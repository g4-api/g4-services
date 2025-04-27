using G4.Models;
using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

using Swashbuckle.AspNetCore.Annotations;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        [Route("disconnect/{connection}")]
        [SwaggerOperation(
            summary: "Stop monitor for a specific bot",
            description: "Sends a StopMonitor command to the connected client identified by the given connection ID.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "StopMonitor command successfully dispatched to the specified bot.")]
        public IActionResult Disconnect(
            [SwaggerParameter(description: "The SignalR connection ID of the bot monitor instance to stop.")] string connection)
        {
            // Send the StopMonitor signal to the client with this connection ID
            _domain.BotsHubContext.Clients.Client(connection).SendAsync("StopMonitor");

            // Return 204 No Content to indicate the command was sent
            return NoContent();
        }

        [HttpDelete]
        [Route("disconnect")]
        [SwaggerOperation(
            summary: "Stop monitors for multiple bots",
            description: "Sends a StopMonitor command to each client identified by the provided connection IDs array.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "StopMonitor commands successfully dispatched to the specified bots.")]
        public IActionResult Disconnect(
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
        [Route("disconnect/all")]
        [SwaggerOperation(
            summary: "Stop monitors for all bots",
            description: "Broadcasts a StopMonitor command to every connected client.",
            Tags = new[] { "Bots" })]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "StopMonitor commands successfully dispatched to all connected bots.")]
        public IActionResult Disconnect()
        {
            // Broadcast the StopMonitor signal to all connected clients
            _domain.BotsHubContext.Clients.All.SendAsync("StopMonitor");

            // Return 204 No Content to indicate the broadcast was sent
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
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Bot successfully registered.",
            type: typeof(ConnectedBotModel),
            contentTypes: [MediaTypeNames.Application.Json])]
        [SwaggerResponse(StatusCodes.Status400BadRequest, description: "Invalid registration payload.")]
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

        //[HttpDelete]
        //[Route("{id}")]
        //[SwaggerOperation(
        //    summary: "Unregister a single bot",
        //    description: "Removes the bot with the specified ID from the domain, provided it is not currently connected.",
        //    Tags = new[] { "Bots" })]
        //[SwaggerResponse(StatusCodes.Status204NoContent, description: "Bot successfully unregistered.")]
        //[SwaggerResponse(StatusCodes.Status404NotFound, description: "No bot found with the given ID.")]
        //public IActionResult Unregister([FromRoute] string id)
        //{
        //    // Check for existence in the domain
        //    if (!_domain.Bots.ConnectedBots.TryGetValue(id, out var connectedBot))
        //    {
        //        // Bot not found: return 404 with a descriptive error
        //        return NotFound(new GenericErrorModel(HttpContext)
        //            .AddError("BotNotFound", $"Bot with ID '{id}' not found."));
        //    }

        //    // Prevent unregistering an actively connected bot
        //    if (!string.IsNullOrEmpty(connectedBot.ConnectionId))
        //    {
        //        throw new InvalidOperationException(
        //            $"Cannot unregister bot '{connectedBot.Name}' (ID='{id}') while it is connected. " +
        //            "Please stop the bot before unregistering."
        //        );
        //    }

        //    // Remove the bot from tracking
        //    _domain.Bots.ConnectedBots.TryRemove(id, out _);

        //    // Return HTTP 204 No Content on success
        //    return NoContent();
        //}

        //[HttpDelete]
        //[SwaggerOperation(
        //    summary: "Unregister multiple bots",
        //    description: "Removes each bot in the provided list of IDs, skipping those that are connected or not found.",
        //    Tags = new[] { "Bots" })]
        //[SwaggerResponse(StatusCodes.Status204NoContent, description: "All valid, disconnected bots have been unregistered.")]
        //public IActionResult Unregister(
        //    [FromBody][SwaggerParameter(description: "Array of bot IDs to unregister.")] string[] ids)
        //{
        //    // Iterate over each ID and attempt removal
        //    foreach (var id in ids)
        //    {
        //        // Attempt to remove each bot if it is not connected
        //        if (_domain.Bots.ConnectedBots.TryGetValue(id, out var bot) && string.IsNullOrEmpty(bot.ConnectionId))
        //        {
        //            _domain.Bots.ConnectedBots.TryRemove(id, out _);
        //        }
        //    }

        //    // Always return 204, as non-existent or connected bots are simply skipped
        //    return NoContent();
        //}

        //[HttpDelete]
        //[Route("all")]
        //[SwaggerOperation(
        //    summary: "Unregister all bots",
        //    description: "Removes every bot in the domain that is not currently connected.",
        //    Tags = new[] { "Bots" })]
        //[SwaggerResponse(StatusCodes.Status204NoContent, description: "All valid, disconnected bots have been unregistered.")]
        //public IActionResult Unregister()
        //{
        //    // Copy keys to avoid modifying collection during enumeration
        //    foreach (var id in _domain.Bots.ConnectedBots.Keys.ToArray())
        //    {
        //        // Attempt to remove each bot if it is not connected
        //        if (_domain.Bots.ConnectedBots.TryGetValue(id, out var bot) && string.IsNullOrEmpty(bot.ConnectionId))
        //        {
        //            _domain.Bots.ConnectedBots.TryRemove(id, out _);
        //        }
        //    }

        //    // Return 204 to signal completion
        //    return NoContent();
        //}

        //[HttpPut]
        //[Route("{id}")]
        //public IActionResult Update([FromRoute] string id, [FromBody] ConnectedBotModel botModel)
        //{
        //    var isBot = _domain.Bots.ConnectedBots.TryGetValue(id, out var bot);
        //    if (!isBot)
        //    {
        //        var error404 = new GenericErrorModel(HttpContext)
        //            .AddError("BotNotFound", $"Bot with ID '{id}' not found.");
        //        return NotFound(error404);
        //    }

        //    bot.Status = botModel.Status;
        //    bot.LastModifiedOn = DateTime.UtcNow;

        //    return NoContent();
        //}

        //[HttpGet]
        //[Route("test/{id}")]
        //[SwaggerOperation(
        //    summary: "Test connectivity for a specific bot",
        //    description: "Checks whether the bot is already connected or can be reached via its callback URI, and updates its status accordingly.",
        //    Tags = new[] { "Bots" })]
        //[SwaggerResponse(StatusCodes.Status200OK, description: "Bot is connected or responded successfully to the test request.")]
        //[SwaggerResponse(StatusCodes.Status410Gone, description: "Bot did not respond or is considered offline.")]
        //public async Task<IActionResult> TestConnection(
        //    [SwaggerParameter(description: "The unique identifier of the bot to test connectivity for.")]
        //    string id)
        //{
        //    // Attempt to retrieve the bot model from the connected bots dictionary
        //    var isBot = _domain.Bots.ConnectedBots.TryGetValue(id, out var bot);

        //    // Determine if the bot already has an active SignalR connection
        //    var isConnected = isBot && !string.IsNullOrEmpty(bot.ConnectionId);

        //    if (isConnected)
        //    {
        //        // Bot is already connected; return HTTP 200 OK immediately
        //        return Ok();
        //    }

        //    // Build an HTTP GET request to the bot's callback endpoint
        //    var error410 = new GenericErrorModel(HttpContext)
        //        .AddError("BotGone", $"Bot with ID '{id}' has been permanently removed.");
        //    var callbackUri = bot.CallbackUri;
        //    using var request = new HttpRequestMessage(HttpMethod.Get, callbackUri);

        //    try
        //    {
        //        // Send the request and await the response
        //        using var response = await s_httpClient.SendAsync(request);

        //        if (response.IsSuccessStatusCode)
        //        {
        //            // On success (2xx), set status to Ready unless already Ready/Working
        //            const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
        //            bot.Status = bot.Status.Equals("Ready", comparison) || bot.Status.Equals("Working", comparison)
        //                ? bot.Status
        //                : "Ready";
        //        }
        //        else
        //        {
        //            // Non-success status code indicates the bot is offline
        //            bot.Status = "Offline";
        //        }

        //        // Return 200 OK if successful, or 410 Gone if not
        //        return response.IsSuccessStatusCode
        //            ? Ok()
        //            : StatusCode(StatusCodes.Status410Gone, error410);
        //    }
        //    catch
        //    {
        //        // On exception (network error, timeout, etc.), mark offline and return 410 Gone
        //        bot.Status = "Offline";
        //        return StatusCode(StatusCodes.Status410Gone, error410);
        //    }
        //}

        //[HttpGet]
        //[Route("test/all")]
        //[SwaggerOperation(
        //    summary: "",
        //    description: "",
        //    Tags = new[] { "Bots" })]
        //[SwaggerResponse(StatusCodes.Status200OK, description: "")]
        //public async Task<IActionResult> TestConnection()
        //{
        //    var testedBots = new List<ConnectedBotModel>();

        //    foreach (var id in _domain.Bots.ConnectedBots.Keys)
        //    {
        //        var isConnected = !string.IsNullOrEmpty(id);
        //        if (isConnected)
        //        {
        //            continue;
        //        }

        //        var bot = _domain.Bots.ConnectedBots[id];
        //        var callbackUri = bot.CallbackUri;
        //        using var request = new HttpRequestMessage(HttpMethod.Get, callbackUri);

        //        try
        //        {
        //            using var response = await s_httpClient.SendAsync(request);

        //            if (response.IsSuccessStatusCode)
        //            {
        //                const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
        //                bot.Status = bot.Status.Equals("Ready", comparison) || bot.Status.Equals("Working", comparison)
        //                    ? bot.Status
        //                    : "Ready";
        //            }
        //            else
        //            {
        //                bot.Status = "Offline";
        //                testedBots.Add(bot);
        //            }
        //        }
        //        catch
        //        {
        //            bot.Status = "Offline";
        //            testedBots.Add(bot);
        //        }
        //    }

        //    return Ok(testedBots);
        //}

        //[HttpPost]
        //[Route("test")]
        //[SwaggerOperation(
        //    summary: "",
        //    description: "",
        //    Tags = new[] { "Bots" })]
        //[SwaggerResponse(StatusCodes.Status200OK, description: "")]
        //public async Task<IActionResult> TestConnection(string[] ids)
        //{
        //    var testedBots = new List<ConnectedBotModel>();

        //    foreach (var id in ids)
        //    {
        //        var isBot = _domain.Bots.ConnectedBots.TryGetValue(id, out var bot);
        //        var isConnected = isBot && !string.IsNullOrEmpty(id);
        //        if (isConnected || !isBot)
        //        {
        //            continue;
        //        }

        //        var callbackUri = bot.CallbackUri;
        //        using var request = new HttpRequestMessage(HttpMethod.Get, callbackUri);

        //        try
        //        {
        //            using var response = await s_httpClient.SendAsync(request);

        //            if (response.IsSuccessStatusCode)
        //            {
        //                const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
        //                bot.Status = bot.Status.Equals("Ready", comparison) || bot.Status.Equals("Working", comparison)
        //                    ? bot.Status
        //                    : "Ready";
        //            }
        //            else
        //            {
        //                bot.Status = "Offline";
        //                testedBots.Add(bot);
        //            }
        //        }
        //        catch
        //        {
        //            bot.Status = "Offline";
        //            testedBots.Add(bot);
        //        }
        //    }

        //    return Ok(testedBots);
        //}
    }
}
