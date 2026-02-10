using G4.Extensions;
using G4.Models;
using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System.Collections.Generic;
using System.Net.Mime;
using System.Text.Json;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/[controller]")]
    [SwaggerTag(description: "Handles automation-related operations in the G4™ Engine.")]
    [ApiExplorerSettings(GroupName = "G4 Hub")]
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
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Successfully invoked the automation session. Returns a dictionary containing detailed information about the automation run.",
            type: typeof(IDictionary<string, G4AutomationResponseModel>),
            contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult Invoke([FromBody] G4AutomationModel automation)
        {
            // Invoke the automation session using the provided automation model.
            var response = _domain.G4.Automation.Invoke(automation);

            // Return a 200 OK response with the detailed automation session results.
            return Ok(response);
        }

        [HttpPost]
        [Route("base64/invoke")]
        [SwaggerOperation(
            summary: "Invokes an automation session using Base64-encoded input.",
            description: "Decodes a Base64-encoded automation model, deserializes it into a G4AutomationModel, and " +
                "triggers the invocation of an automation session. The result includes detailed information about the run, " +
                "returned in JSON format.",
            Tags = ["Automation"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Successfully invoked the automation session using Base64 input. Returns a dictionary with detailed run information.",
            type: typeof(IDictionary<string, G4AutomationResponseModel>),
            contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult InvokeBase64([FromBody] string base64Automation)
        {
            // Convert the Base64-encoded string to JSON.
            var json = base64Automation.ConvertFromBase64();

            // Deserialize the JSON string into a G4AutomationModel using the domain's JSON options.
            var automation = JsonSerializer.Deserialize<G4AutomationModel>(json, _domain.JsonOptions);

            // Invoke the automation session using the deserialized automation model.
            var response = _domain.G4.Automation.Invoke(automation);

            // Return a 200 OK response with the detailed automation session results.
            return Ok(response);
        }

        [HttpPost]
        [Route("init")]
        [SwaggerOperation(
            summary: "Initializes an automation request.",
            description: "Configures and prepares an automation request by setting up all necessary references and action connections in the provided model. The result is an initialized `G4AutomationModel` that is fully configured and ready for invocation.",
            Tags = ["Automation"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Successfully initialized the automation request. Returns the initialized `G4AutomationModel`, ready for invocation.",
            type: typeof(G4AutomationModel),
            contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult Initialize([FromBody] G4AutomationModel automation)
        {
            // Initialize the automation request, setting up references and action connections.
            var response = automation.Initialize();

            // Return a 200 OK response with the initialized G4AutomationModel ready for invocation.
            return Ok(response);
        }

        [HttpPost]
        [Route("resolve")]
        [SwaggerOperation(
            summary: "Resolves macros in the provided G4AutomationModel.",
            description: "Scans and resolves macros for all rules in the G4AutomationModel, returning a list of G4RuleModelBase objects with macros resolved.",
            Tags = ["Automation"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Successfully resolved macros. Returns a list of G4RuleModelBase objects with macros resolved.",
            type: typeof(IEnumerable<G4RuleModelBase>),
            contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult Resolve([FromBody] G4AutomationModel automation)
        {
            // Call the extension method to resolve macros for all rules in the provided automation model.
            var resolvedRules = automation.ResolveMacros();

            // Return a 200 OK response with the resolved rules.
            return Ok(resolvedRules);
        }

        [HttpGet]
        [Route("stop/{automationId}")]
        public IActionResult Stop(string automationId)
        {
            // Stop the automation session with the specified ID.
            var response = _domain.G4.Automation.StopAutomation(automationId);

            // Return a 200 OK response with the stop confirmation.
            return Ok(response);
        }
    }
}
