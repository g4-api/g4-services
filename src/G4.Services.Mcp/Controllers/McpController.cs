using G4.Services.Mcp.Models;

using Microsoft.AspNetCore.Mvc;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace G4.Services.Mcp.Controllers
{
    [Route("/api/v4/g4/[controller]")]
    [ApiController]
    public class McpController : ControllerBase
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        [HttpGet, Route("ping")]
        public IActionResult Ping()
        {
            return Ok("Pong");
        }

        [HttpGet, Route("tools")]
        public IActionResult GetTools()
        {
            // Create mock data for tools for demonstration purposes make it more realistic
            return Ok(new List<ToolDescriptor>
            {
                new ToolDescriptor
                {
                    Name = "create_file",
                    Description = "Create or overwrite a file at the given relative path with the supplied UTF-8 content.",
                    Arguments = new Dictionary<string, Property>
                    {
                        ["RelativePath"] = new(),
                        ["Content"] = new(),
                    },
                    Required= ["RelativePath", "Content"]
                }
            });
        }

        [HttpPost, Route("execute")]
        public async Task<IActionResult> Invoke(object tool)
        {
            await Task.Delay(100); // Simulate some processing delay
            return Ok(new
            {
                relative_path = "example.txt",
                content = "This is an example file content."
            });
        }

        [HttpPost, Route("completions")]
        public IActionResult Comp(CompletionsModel completions)
        {
            completions.FunctionCall = "auto";

            return Ok(new
            {
                message = "Completions endpoint received data successfully.",
            });
        }
    }

    public sealed class ToolDescriptor
    {
        [JsonPropertyName("additionalProperties")]
        public bool AdditionalProperties { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; init; }

        [JsonPropertyName("arguments")]
        public Dictionary<string, Property> Arguments { get; set; }

        [JsonPropertyName("required")]
        public string[] Required { get; set; }

        [JsonPropertyName("requiresConfirmation")]
        public bool RequiresConfirmation { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public sealed class Property
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        [JsonPropertyName("default")]
        public object Default { get; set; }
    }
}
