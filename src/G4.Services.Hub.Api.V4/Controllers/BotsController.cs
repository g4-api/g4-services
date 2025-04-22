using G4.Services.Domain.V4;
using G4.Services.Domain.V4.Hubs;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

using Swashbuckle.AspNetCore.Annotations;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/[controller]")]
    [SwaggerTag(description: "Handles automation-related operations in the G4™ Engine.")]
    public class BotsController(IDomain domain) : ControllerBase
    {
        private readonly IDomain _domain = domain;

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(_domain.ConnectedBots.Values);
        }
    }
}
