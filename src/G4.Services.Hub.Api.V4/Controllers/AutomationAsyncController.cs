using G4.Models;
using G4.Services.Domain.V4;
using G4.Services.Domain.V4.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System.Net.Mime;
using System.Text.Json;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/automation/async")]
    [SwaggerTag(description: "Provides endpoints to manage asynchronous automation tasks within the G4 platform.")]
    [ApiExplorerSettings(GroupName = "G4 Hub")]
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
            var response = _domain.G4.Client.AutomationAsync.Completed;

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
            _domain.G4.Client.AutomationAsync.AddPendingAutomation(automation);

            // Return an HTTP 202 Accepted response indicating that the automation task has been accepted.
            return Accepted();
        }

        [HttpPost]
        [Route("file/start")]
        [SwaggerOperation(
            Summary = "Enqueue a new automation task from a file.",
            Description = "Reads a G4 automation model from the provided file path, deserializes the file content into a G4AutomationModel, and enqueues it into the pending queue. The automation task will be picked up by any available automation worker for asynchronous processing.",
            Tags = ["Automation", "AutomationAsync"])]
        [SwaggerResponse(StatusCodes.Status202Accepted,
            Description = "The automation task has been accepted and enqueued. The response is empty as processing is performed asynchronously.",
            Type = typeof(void),
            ContentTypes = [MediaTypeNames.Application.Json])]
        public IActionResult Start(
            [FromBody]
            [SwaggerRequestBody(
                Description = "The file invocation model containing the path to the automation JSON file.",
                Required = true)]
            FileInvokeModel invokeModel)
        {
            // Read the automation file content from the provided file path.
            var content = System.IO.File.ReadAllText(invokeModel.Path);

            // Deserialize the file content into a G4 automation model using the domain JSON options.
            var automation = JsonSerializer.Deserialize<G4AutomationModel>(content, _domain.Asp.JsonOptions);

            // Enqueue the automation model into the pending queue for asynchronous processing by an available worker.
            _domain.G4.Client.AutomationAsync.AddPendingAutomation(automation);

            // Return an HTTP 202 Accepted response indicating that the automation task has been accepted.
            return Accepted();
        }
    }
}
