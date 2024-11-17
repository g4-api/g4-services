using G4.Extensions;
using G4.Models;
using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System.Collections.Generic;
using System.Net.Mime;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/[controller]")]
    [SwaggerTag(description: "Handles automation-related operations in the G4™ Engine.")]
    public class AutomationController(IDomain domain) : ControllerBase
    {
        // The domain service for the G4™ engine.
        private readonly IDomain _domain = domain;

        [HttpPost]
        [Route("invoke")]
        [SwaggerOperation(
            summary: "Invokes an automation session.",
            description: "Triggers the invocation of an automation session based on the provided automation model. The result includes detailed information about the entire automation run, returned in JSON format.",
            Tags = ["Automation"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully invoked the automation session. Returns a dictionary containing detailed information about the automation run.", type: typeof(IDictionary<string, G4AutomationResponseModel>), contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult Post([FromBody] G4AutomationModel automation)
        {
            // Invoke the automation session using the provided automation model.
            var response = _domain.G4Client.Automation.Invoke(automation);

            // Return a 200 OK response with the detailed automation session results.
            return Ok(response);
        }

        [HttpPost]
        [Route("init")]
        [SwaggerOperation(
            summary: "Initializes an automation request.",
            description: "Configures and prepares an automation request by setting up all necessary references and action connections in the provided model. The result is an initialized `G4AutomationModel` that is fully configured and ready for invocation.",
            Tags = ["Automation"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully initialized the automation request. Returns the initialized `G4AutomationModel`, ready for invocation.", type: typeof(G4AutomationModel), contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult Initialize([FromBody] G4AutomationModel automation)
        {
            // Initialize the automation request, setting up references and action connections.
            var response = automation.Initialize();

            // Return a 200 OK response with the initialized G4AutomationModel ready for invocation.
            return Ok(response);
        }
    }
}
