using Microsoft.AspNetCore.Mvc;

namespace G4.Services.Worker.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PingController : ControllerBase
    {
        [HttpGet, Route("ping")]
        public IActionResult Ping() => Ok("Pong");
    }
}
