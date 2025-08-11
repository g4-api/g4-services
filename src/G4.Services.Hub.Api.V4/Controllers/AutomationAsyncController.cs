using G4.Models;
using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System.Net.Mime;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/automation/async")]
    [SwaggerTag(description: "")]
    public class AutomationAsyncController(IDomain domain) : ControllerBase
    {
        // The domain service for the G4™ engine.
        private readonly IDomain _domain = domain;

        [HttpGet]
        [Route("completed")]
        [SwaggerOperation(
            Summary = "Retrieve completed automation responses.",
            Description = "Fetches the collection of automation responses that have been completed. The responses are managed by the AutomationAsync client.",
            Tags = ["Automation", "AutomationAsync"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            Description = "The completed automation responses were successfully retrieved.",
            Type = typeof(void),
            ContentTypes = [MediaTypeNames.Application.Json])]
        public IActionResult GetCompleted()
        {
            // Retrieve the completed automation responses from the AutomationAsync client.
            var response = _domain.G4.AutomationAsync.Completed;

            // Return a 200 OK response with the completed automation responses.
            return Ok(response);
        }

        [HttpPost]
        [Route("start")]
        [SwaggerOperation(
            Summary = "Enqueue a new automation task",
            Description = "Creates a new automation queue model and enqueues it into the pending queue. The automation task will be picked up by any available automation worker for processing.",
            Tags = ["Automation", "AutomationAsync"])]
        [SwaggerResponse(StatusCodes.Status202Accepted,
            Description = "The automation task has been accepted and enqueued. The response is empty as processing is performed asynchronously.",
            Type = typeof(void),
            ContentTypes = [MediaTypeNames.Application.Json])]
        public IActionResult Start([FromBody] G4AutomationModel automation)
        {
            // Enqueue the automation model into the pending queue for asynchronous processing by an available worker.
            _domain.G4.AutomationAsync.AddPendingAutomation(automation);

            // Return an HTTP 202 Accepted response indicating that the automation task has been accepted.
            return Accepted();
        }
    }
}
