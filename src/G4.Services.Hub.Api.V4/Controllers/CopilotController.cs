using G4.Converters;
using G4.Models;
using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/[controller]")]
    [SwaggerTag(description: "GitHub Copilot Agent endpoint for integration and context exchange with AI agents.")]
    public class CopilotController(IDomain domain) : ControllerBase
    {
        private static JsonSerializerOptions Options
        {
            get
            {
                var options = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    WriteIndented = false
                };

                options.Converters.Add(new TypeConverter());
                options.Converters.Add(new ExceptionConverter());
                options.Converters.Add(new DateTimeIso8601Converter());
                options.Converters.Add(new MethodBaseConverter());

                return options;
            }
        }

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
            [FromBody, Required][SwaggerParameter(description: "...")] CopilotRequestModel copilotRequest)
        {

            static IActionResult NewContentResult(int statusCode, object input)
            {
                var content = JsonSerializer.Serialize(input, Options);

                return new ContentResult
                {
                    StatusCode = statusCode,
                    Content = content,
                    ContentType = MediaTypeNames.Application.Json
                };
            }

            // Acknowledge the initialized notification without further action
            if (copilotRequest.Method == "notifications/initialized")
            {
                return Accepted();
            }

            // Dispatch based on the JSON-RPC method
            return copilotRequest.Method switch
            {
                "initialize" => NewContentResult(statusCode: 200, _domain.Copilot.Initialize(copilotRequest.Id)),
                "notifications/initialized" => Accepted(),
                "tools/list" => NewContentResult(statusCode: 200, _domain.Copilot.GetTools(copilotRequest.Id)),
                "tools/call" => NewContentResult(200, _domain.Copilot.InvokeTool(copilotRequest.Parameters, copilotRequest.Id)),
                _ => NewContentResult(statusCode: 400, new { error = $"Unknown method '{copilotRequest.Method}'" })
            };
        }
    }
}
