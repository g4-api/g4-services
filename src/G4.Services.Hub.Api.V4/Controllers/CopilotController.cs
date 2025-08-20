using G4.Models;
using G4.Services.Domain.V4;
using G4.Services.Domain.V4.Repositories;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/[controller]/mcp")]
    [SwaggerTag(description: "GitHub Copilot Agent endpoint for integration and context exchange with AI agents.")]
    public class CopilotController(IDomain domain) : ControllerBase
    {
        // Dependency injection for domain services
        private readonly IDomain _domain = domain;

        [HttpGet]
        [SwaggerOperation(
            Summary = "Establish SSE stream",
            Description = "Opens a text/event-stream channel for real-time context updates and heartbeats.")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "SSE stream established.", contentTypes: "text/event-stream")]
        public async Task Get(CancellationToken token)
        {
            // Set response headers for SSE
            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "text/event-stream";

            // Send a comment line to initiate the stream connection
            await Response.WriteAsync(": connected\n\n", cancellationToken: token);
            await Response.Body.FlushAsync(token);

            // Loop to send heartbeat comments periodically
            while (!token.IsCancellationRequested)
            {
                // Wait 15 seconds between heartbeats
                await Task.Delay(TimeSpan.FromSeconds(15), token);

                // Send heartbeat comment line to keep the connection alive
                await Response.WriteAsync(": heartbeat\n\n", cancellationToken: token);
                await Response.Body.FlushAsync(token);
            }
        }

        [HttpPost]
        #region *** OpenAPI Documentation ***
        [SwaggerOperation(
            Summary = "Handle Copilot agent requests",
            Description = "Processes JSON-RPC methods for initializing, listing tools, invoking tools, and handling notifications.")]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Initialization result with context (CopilotInitializeResponseModel), list of available tools (CopilotListResponseModel), or result of tool invocation (object)",
            type: typeof(object),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status202Accepted, description: "Initialization notification acknowledged.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest,
                         description: "Invalid or unsupported method.",
                         type: typeof(object),
                         contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult Post(
            [FromBody, Required]
            [SwaggerParameter(description:
                "The Copilot request payload following the JSON-RPC structure. " +
                "It contains the method to invoke (e.g., 'initialize', 'tools/list', 'tools/call'), " +
                "the request identifier, and any required parameters for the method execution.")] CopilotRequestModel copilotRequest)
        {
            // Dispatch based on the JSON-RPC method
            return copilotRequest.Method switch
            {
                "initialize" => NewContentResult(
                    StatusCodes.Status200OK,
                    value: _domain.Copilot.Initialize(copilotRequest.Id),
                    options: ICopilotRepository.G4JsonOptions),
                "notifications/initialized" => Accepted(),
                "tools/list" => NewContentResult(
                    StatusCodes.Status200OK,
                    value: _domain.Copilot.GetTools(copilotRequest.Id, "system-tool")),
                "tools/call" => NewContentResult(
                    StatusCodes.Status200OK,
                    value: _domain.Copilot.InvokeTool(copilotRequest.Parameters, copilotRequest.Id)),
                _ => NewContentResult(
                    StatusCodes.Status400BadRequest,
                    value: new { error = $"Unknown method '{copilotRequest.Method}'" })
            };
        }

        [HttpGet, Route("sync")]
        #region *** OpenAPI Documentation ***
        [SwaggerOperation(
            Summary = "Sync the list of tools available to the Copilot agent",
            Description = "Refreshes the cached tool definitions so that the Copilot agent has the most up-to-date list of tools.")]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "The tools list was successfully synced. No content is returned.")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError,
            description: "An error occurred while syncing the tools list.",
            type: typeof(object),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult SyncTools()
        {
            // Update the list of tools available to the Copilot agent
            _domain.Copilot.SyncTools();

            // Return an empty 204 No Content response
            return NoContent();
        }

        #region *** Methods ***
        // Creates a new ContentResult with a JSON-formatted response body.
        private static ContentResult NewContentResult(int statusCode, object value)
        {
            return NewContentResult(statusCode, value, ICopilotRepository.JsonOptions);
        }

        // Creates a new ContentResult with a JSON-formatted response body.
        private static ContentResult NewContentResult(int statusCode, object value, JsonSerializerOptions options)
        {
            // Serialize the input object to JSON using the repository's predefined serializer options.
            var content = JsonSerializer.Serialize(value, options);

            // Construct and return a ContentResult with the provided status code,
            // serialized JSON content, and the correct content type.
            return new ContentResult
            {
                StatusCode = statusCode,
                Content = content,
                ContentType = MediaTypeNames.Application.Json
            };
        }
        #endregion
    }
}
