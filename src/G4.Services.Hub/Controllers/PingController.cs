using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System.Net.Mime;

namespace G4.Services.Hub.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/[controller]")]
    [SwaggerTag(description: "Provides service identity and API-version metadata for liveness and compatibility checks.")]
    public class PingController : ControllerBase
    {
        [HttpGet]
        [SwaggerOperation(
            summary: "Service metadata endpoint",
            description: "Returns the service identity and active API version for liveness and compatibility checks.")]
        [SwaggerResponse(statusCode: StatusCodes.Status200OK, description: "Service identity and active API version returned.", type: typeof(object), contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult TestConnection() => Ok(new { service = "G4.Services.Hub", version = "v4" });
    }
}
