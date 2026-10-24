using G4.Models;
using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using Swashbuckle.AspNetCore.Annotations;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/[controller]")]
    [SwaggerTag(description: "Endpoints for managing flows within the G4 platform. " +
        "This controller allows for adding, retrieving, and removing flows based on their manifests. " +
        "Flows are complete automations, encoded as Base64, persisted in the Flows collection, and cached by namespace and key " +
        "so clients can discover them and send them for execution as one unit.")]
    [ApiExplorerSettings(GroupName = "G4 Hub")]
    public class FlowsController(IDomain domain) : ControllerBase
    {
        #region *** Fields       ***
        // The domain service whose flows repository owns persistence, caching, and the failure contract shared
        // with the MCP flow tools.
        private readonly IDomain _domain = domain;
        #endregion

        #region *** Methods      ***
        [HttpPut]
        [Consumes(MediaTypeNames.Application.Json)]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Add or overwrite a flow",
            description: "Adds a new flow based on the provided G4FlowManifestModel manifest, whose `automation` property is the Base64 encoding of a G4AutomationModel JSON. If a flow with the same namespace and key exists, it is overwritten and the flows cache is updated. A 204 No Content response indicates a successful addition or overwrite.",
            Tags = ["Flows"])]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "The flow was successfully added or overwritten, and the flows cache was updated.", type: typeof(void))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, description: "Invalid flow. The manifest has no key, or its automation is missing, is not valid Base64, or does not deserialize into an automation.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status409Conflict, description: "Invalid manifest. The flow key or one of its aliases conflicts with another cached flow.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult AddFlow(
            [FromBody][Required][SwaggerRequestBody(description: "The manifest of the flow to be added, with the automation encoded as Base64.", Required = true)] G4FlowManifestModel manifest)
        {
            // Persist and cache the flow; invalid manifests and identity conflicts surface through the middleware.
            _domain.G4.Flows.AddFlow(manifest);

            // Report success once the flow is stored and the flows cache is synchronized.
            return NoContent();
        }

        [HttpDelete]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Clear all flows",
            description: "Removes all flows from the Flows collection and from the flows cache. Returns a 204 No Content response upon successful removal.",
            Tags = ["Flows"])]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "All flows were successfully removed from the collection and the flows cache.", type: typeof(void))]
        #endregion
        public IActionResult ClearFlows()
        {
            // Remove every stored flow; the client clears the flows cache after persistence succeeds.
            _domain.G4.Flows.ClearFlows();

            // Return a 204 No Content response to indicate success.
            return NoContent();
        }

        [HttpGet]
        [Route("{key}")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Retrieve a G4.System flow manifest by key",
            description: "Fetches the manifest of a flow in the G4.System namespace identified by its key. If the flow exists, its manifest is returned in JSON format. If the flow is not found, a 404 Not Found response is returned with a detailed error message.",
            Tags = ["Flows"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Flow manifest found and returned successfully.", type: typeof(G4FlowManifestModel), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Flow not found with the specified key in the G4.System namespace.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetFlow(
            [FromRoute][SwaggerParameter(description: "The key of the G4.System flow to retrieve the manifest for.", Required = true)] string key)
        {
            // Delegate to the namespaced overload; a null namespace selects G4.System.
            return GetFlow(@namespace: null, key);
        }

        [HttpGet]
        [Route("{namespace}/{key}")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Retrieve a flow manifest by namespace and key",
            description: "Fetches the manifest of a flow identified by its namespace and key. If the flow exists, its manifest is returned in JSON format. If the flow is not found, a 404 Not Found response is returned with a detailed error message.",
            Tags = ["Flows"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Flow manifest found and returned successfully.", type: typeof(G4FlowManifestModel), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Flow not found with the specified namespace and key.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetFlow(
            [FromRoute][SwaggerParameter(description: "The namespace of the flow, for example G4.System.", Required = true)] string @namespace,
            [FromRoute][SwaggerParameter(description: "The key of the flow to retrieve the manifest for.", Required = true)] string key)
        {
            // Return the stored manifest; an unknown flow surfaces as 404 through the error middleware.
            return Ok(_domain.G4.Flows.GetFlow(@namespace, key));
        }

        [HttpGet]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Retrieve all flow manifests",
            description: "Fetches all flow manifests, or only the manifests of one namespace when the `namespace` query parameter is provided. The response includes a custom header 'X-Manifest-Count' indicating the total number of manifests returned.",
            Tags = ["Flows"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Manifests retrieved successfully.", type: typeof(G4FlowManifestModel[]), contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetFlows(
            [FromQuery][SwaggerParameter(description: "The optional namespace to filter by. When omitted, flows of every namespace are returned.", Required = false)] string @namespace)
        {
            // Select every flow when no namespace is requested; otherwise, narrow to the requested namespace.
            var manifests = _domain.G4.Flows.GetFlows(@namespace);

            // Append a custom header with the count of manifests.
            Response.Headers.Append("X-Manifest-Count", $"{manifests.Length}");

            // Return the array of manifests with a 200 OK response.
            return Ok(manifests);
        }

        [HttpPost]
        [Route("{key}/invoke")]
        [Consumes(MediaTypeNames.Application.Json)]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Invoke a G4.System flow by key",
            description: "Runs the flow in the G4.System namespace identified by its key or alias, and waits for it to finish. " +
                "Parameter values replace the `{{$ parameters.Name }}` placeholders anywhere in the flow, as a template's argument does: " +
                "send them as a `parameters` object, as an `argument` in command-line form (`{{$ --Name:value}}`), or both; on a name in both, the `argument` value is used. " +
                "A declared parameter without a value receives its `default`; a placeholder without a value stays as it is. Values are inserted as they are, so a value with a quote or a backslash can make the flow invalid (400). " +
                "The caller's `authentication.token` always replaces the flow's token; the caller's other `authentication`, `driverParameters`, and `settings` values only fill what the flow leaves out. " +
                "Set `referenceId` to a unique value to choose the run's automation ID up front, so it can be stopped with DELETE /flows/invocations/{automationId}; otherwise the flow's own ID, or a generated one, is used. " +
                "The body is optional. Example body: `{ \"parameters\": { \"UserName\": \"admin\" }, \"argument\": \"{{$ --Retries:3}}\", \"referenceId\": \"my-run-1\" }`.",
            Tags = ["Flows"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "The flow ran. Returns a dictionary of automation responses by group ID, as automation/invoke does.", type: typeof(IDictionary<string, G4AutomationResponseModel>), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status400BadRequest, description: "Invalid flow. The stored automation cannot be decoded, it is not valid JSON after its parameters were applied, or its data source cannot be read (InvalidDataSource).", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Flow not found with the specified key in the G4.System namespace.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult InvokeFlow(
            [FromRoute][SwaggerParameter(description: "The key or alias of the G4.System flow to invoke.", Required = true)] string key,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)][SwaggerRequestBody(description: "The parameter values and the caller's authentication, driver parameters, and settings.", Required = false)] G4FlowInvokeModel invokeModel)
        {
            // Delegate to the namespaced overload; a null namespace selects G4.System.
            return InvokeFlow(@namespace: null, key, invokeModel);
        }

        [HttpPost]
        [Route("{namespace}/{key}/invoke")]
        [Consumes(MediaTypeNames.Application.Json)]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Invoke a flow by namespace and key",
            description: "Runs the flow identified by its namespace and key or alias, and waits for it to finish. " +
                "Parameter values replace the `{{$ parameters.Name }}` placeholders anywhere in the flow, as a template's argument does: " +
                "send them as a `parameters` object, as an `argument` in command-line form (`{{$ --Name:value}}`), or both; on a name in both, the `argument` value is used. " +
                "A declared parameter without a value receives its `default`; a placeholder without a value stays as it is. Values are inserted as they are, so a value with a quote or a backslash can make the flow invalid (400). " +
                "The caller's `authentication.token` always replaces the flow's token; the caller's other `authentication`, `driverParameters`, and `settings` values only fill what the flow leaves out. " +
                "Set `referenceId` to a unique value to choose the run's automation ID up front, so it can be stopped with DELETE /flows/invocations/{automationId}; otherwise the flow's own ID, or a generated one, is used. " +
                "The body is optional. Example body: `{ \"parameters\": { \"UserName\": \"admin\" }, \"argument\": \"{{$ --Retries:3}}\", \"referenceId\": \"my-run-1\" }`.",
            Tags = ["Flows"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "The flow ran. Returns a dictionary of automation responses by group ID, as automation/invoke does.", type: typeof(IDictionary<string, G4AutomationResponseModel>), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status400BadRequest, description: "Invalid flow. The stored automation cannot be decoded, it is not valid JSON after its parameters were applied, or its data source cannot be read (InvalidDataSource).", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Flow not found with the specified namespace and key.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult InvokeFlow(
            [FromRoute][SwaggerParameter(description: "The namespace of the flow, for example G4.System.", Required = true)] string @namespace,
            [FromRoute][SwaggerParameter(description: "The key or alias of the flow to invoke.", Required = true)] string key,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)][SwaggerRequestBody(description: "The parameter values and the caller's authentication, driver parameters, and settings.", Required = false)] G4FlowInvokeModel invokeModel)
        {
            // Run the flow and return its responses; unknown and invalid flows surface through the error middleware.
            return Ok(_domain.G4.Flows.InvokeFlow(@namespace, key, invokeModel));
        }

        [HttpDelete]
        [Route("{key}")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Remove a G4.System flow by key",
            description: "Removes the flow in the G4.System namespace identified by its key from the Flows collection and the flows cache. If the flow is successfully removed, a 204 No Content response is returned. If the flow is not found, a 404 Not Found response is returned with an error message.",
            Tags = ["Flows"])]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "The flow was successfully removed from the collection and the flows cache.", type: typeof(void))]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Flow not found with the specified key in the G4.System namespace.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult RemoveFlow(
            [FromRoute][SwaggerParameter(description: "The key of the G4.System flow to be removed.", Required = true)] string key)
        {
            // Delegate to the namespaced overload; a null namespace selects G4.System.
            return RemoveFlow(@namespace: null, key);
        }

        [HttpDelete]
        [Route("{namespace}/{key}")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Remove a flow by namespace and key",
            description: "Removes the flow identified by its namespace and key from the Flows collection and the flows cache. If the flow is successfully removed, a 204 No Content response is returned. If the flow is not found, a 404 Not Found response is returned with an error message.",
            Tags = ["Flows"])]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "The flow was successfully removed from the collection and the flows cache.", type: typeof(void))]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Flow not found with the specified namespace and key.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult RemoveFlow(
            [FromRoute][SwaggerParameter(description: "The namespace of the flow, for example G4.System.", Required = true)] string @namespace,
            [FromRoute][SwaggerParameter(description: "The key of the flow to be removed.", Required = true)] string key)
        {
            // Remove the flow; an unknown flow surfaces as 404 through the error middleware.
            _domain.G4.Flows.RemoveFlow(@namespace, key);

            // Return 204 No Content once the flow is removed from the collection and the flows cache.
            return NoContent();
        }

        [HttpDelete]
        [Route("invocations/{automationId}")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Stop a running flow invocation",
            description: "Stops the running flow invocation identified by its automation ID (the `referenceId` sent to invoke, or the flow's own reference ID): a cancellation signal is sent and its driver sessions are closed, " +
                "so the flow stops at its next step. Returns the same result as automation/stop: 0 when the stop request completed, 1 when an error occurred.",
            Tags = ["Flows"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "The stop request was handled. Returns 0 on success or 1 on error.", type: typeof(int), contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult StopFlow(
            [FromRoute][SwaggerParameter(description: "The automation ID of the running flow invocation to stop.", Required = true)] string automationId)
        {
            // Stop the flow invocation with the specified automation ID.
            var response = _domain.G4.Flows.StopInvocation(automationId);

            // Return a 200 OK response with the stop result.
            return Ok(response);
        }
        #endregion
    }
}
