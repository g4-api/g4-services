using G4.Models;
using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System.Net.Mime;
using System.Threading.Tasks;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/[controller]")]
    [SwaggerTag(description: "Provides endpoints for interacting with the OpenAI-compatible backend, including model listing, " +
        "chat completions, health status, and embeddings. All responses are standardized for G4 internal use.")]
    [ApiExplorerSettings(GroupName = "G4 Hub")]
    public class OpenAiController(IDomain domain) : ControllerBase
    {
        private readonly IDomain _domain = domain;

        [HttpGet]
        [Route("models")]
        [SwaggerOperation(
            Summary = "List available models",
            Description = "Returns all available models from the OpenAI-compatible backend, prefixed with `g4-` for internal use.",
            Tags = ["OpenAi", "Models"])]
        [SwaggerResponse(StatusCodes.Status200OK, Description = "Successfully retrieved list of available models.", Type = typeof(OpenAiModelListResponse), ContentTypes = [MediaTypeNames.Application.Json])]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, Description = "Bad request or error occurred while fetching the models.", Type = typeof(GenericErrorModel), ContentTypes = [MediaTypeNames.Application.Json])]
        public async Task<IActionResult> GetModels()
        {
            // Retrieve the models from the OpenAI-compatible service via the domain layer.
            var modelsResult = await _domain.OpenAi.GetModelsAsync();

            // Return a 400+ status with error details if the backend call failed.
            // Return the list of models with a 200 OK response.
            return modelsResult.StatusCode >= 400
                ? StatusCode(statusCode: modelsResult.StatusCode, value: modelsResult.Error)
                : Ok(value: modelsResult.Response);
        }

        [HttpGet]
        [SwaggerOperation(
            Summary = "Check proxy health status",
            Description = "Returns a basic status object indicating that the OpenAI proxy is online.",
            Tags = ["OpenAi", "Status"])]
        [SwaggerResponse(StatusCodes.Status200OK, Description = "The service is operational.", Type = typeof(object), ContentTypes = [MediaTypeNames.Application.Json])]
        public IActionResult GetStatus()
        {
            // Return a 200 OK response indicating the service is running.
            return Ok(new
            {
                Object = "openai_proxy",
                Status = "ok"
            });
        }

        [HttpPost]
        [Route("chat/completions")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [SwaggerOperation(
            Summary = "Generate chat completions",
            Description = "Sends a chat completion request to the OpenAI-compatible backend. Supports streaming or non-streaming responses based on the `stream` flag.",
            Tags = ["OpenAi", "ChatCompletions"])]
        [SwaggerResponse(StatusCodes.Status200OK, Description = "Returns the full chat completion if `stream=false`.", ContentTypes = [MediaTypeNames.Application.Json])]
        [SwaggerResponse(StatusCodes.Status204NoContent, Description = "The chat completion is streamed directly to the response if `stream=true`.")]
        public async Task<IActionResult> SendCompletions([FromBody] OpenAiChatCompletionRequest completions)
        {
            if (completions.Stream)
            {
                // If streaming is enabled, write to the HTTP response stream directly.
                await _domain.OpenAi.SendCompletionsStreamAsync(HttpContext.Response, completions);

                // Indicate that no content will be returned from the controller (streaming handled directly).
                return new EmptyResult();
            }

            // If not streaming, get the full response as a single JSON payload.
            var content = await _domain.OpenAi.SendCompletionsAsync(completions);

            // Return the full completion response with 200 OK.
            return new ContentResult
            {
                Content = content,
                ContentType = MediaTypeNames.Application.Json,
                StatusCode = StatusCodes.Status200OK
            };
        }

        [HttpPost, Route("completions")]
        [SwaggerOperation(
            Summary = "",
            Description = "",
            Tags = ["OpenAi", "ChatCompletions"])]
        public IActionResult ChatLegacy()
        {
            return Ok();
        }

        [HttpPost, Route("chat/embeddings")]
        [SwaggerOperation(
            Summary = "",
            Description = "",
            Tags = ["OpenAi", "Embeddings"])]
        public IActionResult Embeddings()
        {
            return Ok();
        }
    }
}
