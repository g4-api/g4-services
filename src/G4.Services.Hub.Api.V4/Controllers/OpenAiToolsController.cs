using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/tools")]
    [ApiExplorerSettings(GroupName = "OpenAi Tools")]
    public class OpenAiToolsController : ControllerBase
    {
        // Tools list
        // 1. New Automation Flow
        // 2. Test Automation Action
        // 3. Approve Automation Flow
        // 4. Register Automation Flow
        // 5. Call Automation Flow

        [HttpPost("write_file")]
        [Consumes("application/json")]
        [SwaggerOperation(summary: "Writes the specified content to a file at the given path.", OperationId = "WriteFile", Tags = ["AiTools"])]
        [SwaggerResponse(statusCode: 200, description: "Success or fail result", Type = typeof(WriteFileResponse), ContentTypes = [MediaTypeNames.Application.Json])]
        public async Task<IActionResult> WriteFile(
            [FromBody]
            [SwaggerRequestBody(description: "File creation and content information", Required = true)] WriteFileRequest request)
        {
            var a = "stop here";

            Thread.Sleep(5000);

            return Ok(new
            {
                status = $"File at 'some location' created successfully"
            });
        }
    }

    [SwaggerSchema(description: "File creation and content information")]
    public class WriteFileRequest
    {
        [Required]
        [SwaggerSchema(description: "The full path to the file to write. Intermediate directories must already exist.")]
        public string Path { get; set; }

        [Required]
        [SwaggerSchema(description: "The text content to write to the file.")]
        public string Content { get; set; }

        [SwaggerSchema(description: "Whether to overwrite the file if it already exists.")]
        public bool Overwrite { get; set; } = false;
    }

    public class WriteFileResponse
    {
        public string Status { get; set; } = string.Empty;
    }
}
