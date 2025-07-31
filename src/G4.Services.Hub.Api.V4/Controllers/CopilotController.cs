using G4.Models;
using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/[controller]")]
    [SwaggerTag(description: "GitHub Copilot Agent endpoint for integration and context exchange with AI agents.")]
    public class CopilotController(IDomain domain) : ControllerBase
    {
        // Dependency injection for domain services
        private readonly IDomain _domain = domain;

        [HttpGet]
        [SwaggerOperation(
            Summary = "Establish SSE stream",
            Description = "Opens a text/event-stream channel for real-time context updates and heartbeats.")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "SSE stream established.", contentTypes: ["text/event-stream"])]
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
            contentTypes: [MediaTypeNames.Application.Json])]
        [SwaggerResponse(StatusCodes.Status202Accepted,
                         description: "Initialization notification acknowledged.",
                         contentTypes: [])]
        [SwaggerResponse(StatusCodes.Status400BadRequest,
                         description: "Invalid or unsupported method.",
                         type: typeof(object),
                         contentTypes: [MediaTypeNames.Application.Json])]
        #endregion
        public IActionResult Post(
            [FromBody, Required][SwaggerParameter(description:"...")] CopilotRequestModel copilotRequest)
        {
            // Acknowledge the initialized notification without further action
            if (copilotRequest.Method == "notifications/initialized")
            {
                return Accepted();
            }

            // Dispatch based on the JSON-RPC method
            return copilotRequest.Method switch
            {
                "initialize" => Ok(_domain.Copilot.Initialize(copilotRequest.Id)),
                "notifications/initialized" => Accepted(),
                "tools/list" => Ok(_domain.Copilot.GetTools(copilotRequest.Id)),
                "tools/call" => Ok(_domain.Copilot.InvokeTool(copilotRequest.Parameters, copilotRequest.Id)),
                _ => BadRequest(new { error = $"Unknown method '{copilotRequest.Method}'" })
            };
        }
    }
}
