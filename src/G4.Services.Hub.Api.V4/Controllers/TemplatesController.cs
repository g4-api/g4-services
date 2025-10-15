using G4.Attributes;
using G4.Models;
using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mime;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("api/v4/g4/[controller]")]
    [ApiExplorerSettings(GroupName = "G4 Hub")]
    public class TemplatesController(IDomain domain) : ControllerBase
    {
        // The domain service instance is injected into the controller
        private readonly IDomain _domain = domain;

        [HttpPut]
        [SwaggerOperation(
            summary: "Add or overwrite a template",
            description: "Adds a new template based on the provided G4PluginAttribute manifest. If the template exists, it is overwritten and the engine cache is updated. A 204 No Content response indicates a successful addition or overwrite. If an error occurs, an appropriate error response is returned.",
            Tags = ["Templates"])]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "The template was successfully added or overwritten, and the engine cache was updated.", type: typeof(void))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, description: "Invalid request. The manifest provided is not in the correct format or missing required fields.", type: typeof(GenericErrorModel))]
        [SwaggerResponse(StatusCodes.Status409Conflict, description: "Invalid manifest. The provided manifest could not be processed.", type: typeof(GenericErrorModel))]
        public IActionResult AddTemplate(
            [FromBody][Required][SwaggerParameter(description: "The manifest of the template to be added.", Required = true)] G4PluginAttribute manifest)
        {
            try
            {
                // Validate the manifest to ensure it contains the required source property
                manifest.Source = manifest.Source.Equals("Template", StringComparison.OrdinalIgnoreCase)
                    ? manifest.Source
                    : "Template";

                // Attempt to add the new template using the provided manifest
                _domain.G4.Templates.AddTemplate(manifest);

                // If the template was successfully added or overwritten, return 204 No Content
                return NoContent();
            }
            catch (Exception e) when (e.GetBaseException() is InvalidOperationException baseException)
            {
                // Create a generic error model and add an error message indicating the manifest is invalid
                var error409 = new GenericErrorModel(HttpContext).AddError(name: "InvalidManifest", value: baseException.Message);

                // Return a 409 Conflict response with the error details
                return Conflict(error409);
            }
        }

        [HttpDelete]
        [SwaggerOperation(
            summary: "Clear all templates",
            description: "Removes all templates from the cache. Returns a 204 No Content response upon successful removal.",
            Tags = ["Environments"])]
        [Produces(MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "All templates were successfully cleared from the cache.", type: typeof(void))]
        public IActionResult ClearTemplates()
        {
            // Clear all templates from the cache
            _domain.G4.Templates.ClearTemplates();

            // Return a 204 No Content response to indicate success
            return NoContent();
        }

        [HttpGet("{key}")]
        [SwaggerOperation(
            summary: "Retrieve a template manifest by key",
            description: "Fetches the manifest of a template identified by its key. If the template exists, its manifest is returned in JSON format. If the template is not found, a 404 Not Found response is returned with a detailed error message.",
            Tags = ["Templates"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Template manifest found and returned successfully.", type: typeof(G4PluginAttribute), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Template not found with the specified key.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult GetTemplate(
            [FromRoute][SwaggerParameter(description: "The key of the template to retrieve the manifest for.", Required = true)] string key)
        {
            // Retrieve the manifest of the specified template by key
            var (statusCode, manifest) = _domain.G4.Templates.GetTemplate(key);

            // If the template is found, return a 200 OK response with the manifest in JSON format
            if (statusCode == StatusCodes.Status200OK)
            {
                return Ok(manifest);
            }

            // Initialize an error model with a message indicating the template was not found
            var error404 = new GenericErrorModel(HttpContext).AddError(
                name: "TemplateNotFound",
                value: $"The template with key '{key}' was not found in the cache. Please try re-adding the template or reload the service.");

            // Return a 404 Not Found response with an error message
            return NotFound(error404);
        }

        [HttpGet]
        [SwaggerOperation(
            summary: "Retrieve all template manifests",
            description: "Fetches all template manifests available in the system. The response includes a custom header 'X-Manifest-Count' indicating the total number of manifests returned.",
            Tags = ["Templates"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Manifests retrieved successfully.", type: typeof(G4PluginAttribute[]), contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult GetTemplates()
        {
            var manifests = _domain.G4.Templates.GetTemplates().ToArray();

            // Append a custom header with the count of manifests
            Response.Headers.Append("X-Manifest-Count", $"{manifests.Length}");

            // Return the array of manifests with a 200 OK response
            return Ok(manifests);
        }

        [HttpDelete]
        [Route("{key}")]
        [SwaggerOperation(
            summary: "Remove a template by key",
            description: "Removes the specified template identified by its key from the cache. If the template is successfully removed, a 204 No Content response is returned. If the template is not found, a 404 Not Found response is returned with an error message.",
            Tags = ["Environments"])]
        [Produces(MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "The template was successfully removed from the cache.", type: typeof(void))]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Template not found with the specified key.", type: typeof(GenericErrorModel))]
        public IActionResult RemoveTemplate(
            [FromRoute][SwaggerParameter(description: "The key of the template to be removed.", Required = true)] string key)
        {
            // Remove the template from the cache
            var statusCode = _domain.G4.Templates.RemoveTemplate(key);

            // If the template was successfully removed, return 204 No Content
            if (statusCode == StatusCodes.Status204NoContent)
            {
                return NoContent();
            }

            // Initialize an error model with a message indicating the template was not found
            var error404 = new GenericErrorModel(HttpContext).AddError(
                name: "TemplateNotFound",
                value: $"The template with key '{key}' was not found in the cache. Please try re-adding the template or reload the service.");

            // Return a 404 Not Found response with an error message
            return NotFound(error404);
        }
    }
}
