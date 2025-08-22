using G4.Models.Schema;
using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System.Net.Mime;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/tools")]
    [ApiExplorerSettings(GroupName = "OpenAi Tools")]
    public class OpenAiToolsController(IDomain domain) : ControllerBase
    {
        private readonly IDomain _domain = domain;

        [HttpPost("find_tool")]
        #region *** OpenApi Documentation ***
        [Consumes("application/json")]
        [SwaggerOperation(
            summary: "Finds a tool by name or intent.",
            description: "Attempts to locate a tool in the registry. If the tool name is not provided or is inaccurate, the intent will be used as a fallback for vector-based lookup.",
            OperationId = "FindTool",
            Tags = ["AiTools"]
        )]
        [SwaggerResponse(
            statusCode: StatusCodes.Status200OK,
            description: "The tool was found successfully. Returns a ToolOutputSchema containing the matched tool details.",
            Type = typeof(ToolOutputSchema),
            ContentTypes = [MediaTypeNames.Application.Json]
        )]
        #endregion
        public IActionResult FindTool(
            [FromBody]
            [SwaggerRequestBody(
                description: "The input schema containing the tool name (optional) and/or intent (used for fallback lookup).",
                Required = true
            )]
            FindToolInputSchema request)
        {
            // Attempt to locate the tool using the provided intent and/or tool name.
            var result = _domain.Tools.FindTool(request.Intent, request.ToolName);

            // Return the result wrapped in the standard output schema.
            return Ok(new ToolOutputSchema
            {
                Result = result
            });
        }
    }
}
