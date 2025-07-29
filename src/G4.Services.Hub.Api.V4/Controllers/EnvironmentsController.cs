using G4.Extensions;
using G4.Models;
using G4.Services.Domain.V4;
using G4.Services.Domain.V4.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Text.Json;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/[controller]")]
    [SwaggerTag(description: "Provides endpoints to manage environments and their associated parameters, including creating, retrieving, updating, and deleting parameters within specific environments.")]
    public class EnvironmentsController(IDomain domain) : ControllerBase
    {
        // The domain service instance is injected into the controller
        private readonly IDomain _domain = domain;

        [HttpDelete]
        [SwaggerOperation(
            summary: "Clear all environments",
            description: "Removes all environments and their associated parameters. Returns a 204 No Content response upon successful removal.",
            Tags = ["Environments"])]
        [Produces(MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "All environments were successfully cleared.", type: typeof(void))]
        public IActionResult ClearEnvironments()
        {
            // Attempt to clear all environments
            _domain.G4.Environments.ClearEnvironments();

            // Return a 204 No Content response to indicate success
            return NoContent();
        }

        [HttpGet]
        [SwaggerOperation(
            summary: "Retrieve all environments with global parameters",
            description: "Fetches a list of environments, where each environment contains a collection of key-value pairs representing global configuration parameters.",
            Tags = ["Environments"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "A dictionary where each environment is mapped to its corresponding global configuration parameters.", type: typeof(Dictionary<string, IDictionary<string, object>>), contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult GetEnvironments()
        {
            // Retrieve environments and their global parameters from the domain service
            var environments = _domain.G4.Environments.GetEnvironments();

            // Return a 200 OK response with the environments and their global parameters in JSON format
            return Ok(environments);
        }

        [HttpGet]
        [Route("{name}")]
        [SwaggerOperation(
            summary: "Retrieve details of an environment by name",
            description: "Fetches detailed information of a specified environment by name. If the environment exists, the details are returned in JSON format.",
            Tags = ["Environments"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Environment found and returned successfully.", type: typeof(Dictionary<string, object>), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Environment not found with the specified name.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult GetEnvironment(
            [FromRoute][SwaggerParameter(description: "The name of the environment to retrieve.", Required = true)] string name,
            [FromQuery][SwaggerParameter(description: "Specifies whether the parameters values should be Base64-decoded before being returned.", Required = false)] bool decode = false)
        {
            // Retrieve the environment object based on the provided
            // name (note: case-sensitivity depends on how 'GetEnvironment' handles it)
            var environment = _domain.G4.Environments.GetEnvironment(name, decode);

            // If the environment is not found, return a 404 Not Found response along with an error message
            if (environment == null)
            {
                return NotFound(new GenericErrorModel(HttpContext)
                    .AddError(name: "EnvironmentNotFound", value: $"The environment with the name '{name}' was not found."));
            }

            // If the environment is found, return a 200 OK
            // response with the environment details in JSON format
            return Ok(environment);
        }

        [HttpGet]
        [Route("{environment}/parameter/{name}")]
        [SwaggerOperation(
            summary: "Retrieve a parameter from the specified environment",
            description: "Fetches the value of a parameter from the specified environment. The value can optionally be Base64-decoded before being returned.",
            Tags = ["Environments"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "The parameter was found and its value is returned as plain text.", type: typeof(ParameterResponseModel), contentTypes: MediaTypeNames.Text.Plain)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "The parameter was not found in the specified environment.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult GetParameter(
            [FromRoute][SwaggerParameter(description: "The name of the environment where the parameter is located.", Required = true)] string environment,
            [FromRoute][SwaggerParameter(description: "The name of the parameter to retrieve.", Required = true)] string name,
            [FromQuery][SwaggerParameter(description: "Specifies whether the parameter value should be Base64-decoded before being returned.", Required = false)] bool decode = false)
        {
            // Retrieve the parameter value from the specified environment and optionally decode it
            var value = _domain.G4.Environments.GetParameter(environment, name, decode);

            // If the value is null or empty, return a 404 Not Found response with an error message
            // Otherwise, return a 200 OK response with the parameter value
            return string.IsNullOrEmpty(value)
                ? NotFound(new GenericErrorModel(HttpContext).AddError(name, $"Parameter not found in environment '{environment}'."))
                : Ok(value);
        }

        [HttpDelete]
        [Route("{name}")]
        [SwaggerOperation(
            summary: "Delete an environment by name",
            description: "Removes the specified environment by name. If the environment is successfully deleted, a 204 No Content response is returned. If the environment is not found, a 404 Not Found response is returned.",
            Tags = ["Environments"])]
        [Produces(MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "The environment was successfully deleted.", type: typeof(void))]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Environment not found with the specified name.", type: typeof(GenericErrorModel))]
        public IActionResult RemoveEnvironment(
            [FromRoute][SwaggerParameter(description: "The name of the environment to be removed.", Required = true)] string name)
        {
            // Attempt to remove the specified environment
            var statusCode = _domain.G4.Environments.RemoveEnvironment(name);

            if (statusCode == StatusCodes.Status404NotFound)
            {
                // If the environment is not found, return a 404 Not Found response with an error message
                return NotFound(new GenericErrorModel(HttpContext)
                    .AddError(name: "EnvironmentNotFound", value: $"The environment with the name '{name}' was not found."));
            }

            // If the environment was successfully removed, return a 204 No Content response
            return NoContent();
        }

        [HttpDelete]
        [Route("{environment}/parameter/{name}")]
        [SwaggerOperation(
            summary: "Delete a parameter from the specified environment",
            description: "Removes a parameter from the specified environment. If the parameter is found and successfully deleted, a 204 No Content response is returned. If the parameter is not found, a 404 Not Found response is returned.",
            Tags = ["Environments"])]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "The parameter was successfully deleted from the environment.", type: typeof(void), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "The parameter was not found in the specified environment.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult RemoveParameter(
            [FromRoute][SwaggerParameter(description: "The name of the environment from which the parameter will be deleted.", Required = true)] string environment,
            [FromRoute][SwaggerParameter(description: "The name of the parameter to delete.", Required = true)] string name)
        {
            // Attempt to delete the parameter from the specified environment
            var statusCode = _domain.G4.Environments.RemoveParameter(environment, name);

            // If the deletion was successful, return a 204 No Content response
            if (statusCode == StatusCodes.Status204NoContent)
            {
                return NoContent();
            }

            // If the parameter was not found, return a 404 Not Found response with an error message
            var error404 = new GenericErrorModel(HttpContext)
                .AddError(name, $"Parameter '{name}' not found in environment '{environment}'.");

            // Return 404 with the detailed error message
            return NotFound(error404);
        }

        [HttpPut]
        [Route("{name}")]
        [SwaggerOperation(
            summary: "Update an existing environment or create a new one",
            description: "Updates the parameters of an existing environment identified by its name. If the environment does not exist, it will be created with the provided parameters. A 204 No Content response indicates a successful update, while a 201 Created response indicates that a new environment was created.",
            Tags = ["Environments"])]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status201Created, description: "The environment did not exist and was successfully created with the provided parameters.", type: typeof(Dictionary<string, object>))]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "The environment existed and was successfully updated with the provided parameters.", type: typeof(void))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, description: "Invalid request. The environment name is not in the correct format.", type: typeof(GenericErrorModel))]
        public IActionResult SetEnvironment(
            [FromRoute][Required][RegularExpression(@"\w+")][SwaggerParameter(description: "The name of the environment to be updated or created.", Required = true)] string name,
            [FromBody][SwaggerParameter(description: "A dictionary of parameters to associate with the environment. If the environment exists, these parameters will be updated. If not, a new environment will be created with these parameters.", Required = false)] Dictionary<string, string> parameters,
            [FromQuery][SwaggerParameter(description: "Specifies whether the parameters should be Base64-encoded before being stored. Defaults to true.", Required = false)] bool encode = true)
        {
            // Ensure the parameters dictionary is initialized to prevent null reference issues
            parameters ??= [];

            // Attempt to update the environment if it exists, or create it if it does not
            var statusCode = _domain.G4.Environments.SetEnvironment(name, parameters, encode);

            if (statusCode == 204)
            {
                // If the environment was updated successfully, return a 204 No Content response
                return NoContent();
            }

            // If the environment was created successfully, return a 201 Created response with the environment parameters
            return Created(uri: $"{HttpContext.Request.Path}", value: parameters);
        }

        [HttpPut]
        [Route("parameter/{name}")]
        [SwaggerOperation(
            summary: "Create or update a system parameter",
            description: "Creates a new parameter or updates an existing one under the default environment 'SystemParameters'. Optionally encodes the value in Base64 before storing.",
            Tags = ["Environments"])]
        [Consumes(MediaTypeNames.Text.Plain)]
        [Produces(MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Parameter successfully updated in 'SystemParameters'. Returns the updated parameter.", type: typeof(ParameterResponseModel))]
        [SwaggerResponse(StatusCodes.Status201Created, description: "Parameter successfully created in 'SystemParameters'. Returns the newly created parameter.", type: typeof(ParameterResponseModel))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, description: "Invalid request. The parameter name does not conform to the required format or other validation errors occurred.", type: typeof(GenericErrorModel))]
        public IActionResult SetParameter(
            [FromRoute][SwaggerParameter(description: "The name of the parameter to be created or updated in 'SystemParameters'.", Required = true)] string name,
            [FromBody][SwaggerParameter(description: "The value of the parameter. If not provided, the parameter will be set with an empty value.", Required = false)] string value,
            [FromQuery][SwaggerParameter(description: "Specifies whether the value should be encoded to Base64 before being stored. Defaults to true.", Required = false)] bool encode = true)
        {
            // Create a new parameter model with the provided values, under the default "SystemParameters" environment
            var parameter = new EnvironmentParameterModel
            {
                Encode = encode,
                EnvironmentName = string.Empty,
                Name = name,
                Value = value
            };

            // Delegate the parameter setting logic to another method
            return SetParameter(parameter);
        }

        [HttpPut]
        [Route("{environment}/parameter/{name}")]
        [SwaggerOperation(
            summary: "Create or update a parameter in the specified environment",
            description: "This operation creates a new parameter or updates an existing one in the specified environment. If the parameter doesn't exist, it will be created. The value can optionally be Base64-encoded before storage.",
            Tags = ["Environments"])]
        [Consumes(MediaTypeNames.Text.Plain)]
        [Produces(MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Parameter existed and was successfully updated.", type: typeof(ParameterResponseModel))]
        [SwaggerResponse(StatusCodes.Status201Created, description: "Parameter did not exist and was successfully created.", type: typeof(ParameterResponseModel))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, description: "Invalid request. The parameter name or environment does not conform to the required format.", type: typeof(GenericErrorModel))]
        public IActionResult SetParameter(
            [FromRoute][SwaggerParameter(description: "The name of the environment where the parameter will be set or updated. If not provided, a default environment will be used.", Required = true)] string environment,
            [FromRoute][SwaggerParameter(description: "The name of the parameter to be created or updated.", Required = true)] string name,
            [FromBody][SwaggerParameter(description: "The value of the parameter. If not provided, the parameter will be set with an empty value.", Required = false)] string value,
            [FromQuery][SwaggerParameter(description: "Specifies whether the value should be Base64-encoded before being stored. Defaults to true.", Required = false)] bool encode = true)
        {
            // Create a new parameter model based on the provided values
            var parameter = new EnvironmentParameterModel
            {
                Encode = encode,
                EnvironmentName = environment,
                Name = name,
                Value = value
            };

            // Delegate the parameter setting logic to another method
            return SetParameter(parameter);
        }

        // Sets an environment parameter after validating it.
        private IActionResult SetParameter(EnvironmentParameterModel parameter)
        {
            // Initialize a list to hold validation results
            var (isValid, validationResults) = parameter.Confirm();

            // If the parameter object is not valid, return a 400 Bad Request response with the validation errors
            if (!isValid)
            {
                // Clear the current model state to remove any previous errors
                ModelState.Clear();

                // Create a generic error model and add validation errors to it
                var error400 = new GenericErrorModel(HttpContext).AddErrors(validationResults);

                // Return a 400 Bad Request response with the error model
                return BadRequest(error400);
            }

            // Attempt to set the parameter in the domain environment
            var (statusCode, parameterValue) = _domain.G4.Environments.SetParameter(parameter);

            // Determine the appropriate success message based on the status code
            var environmentName = string.IsNullOrEmpty(parameter.EnvironmentName) ? "SystemParameters" : parameter.EnvironmentName;
            var message = statusCode == 201
                ? $"Parameter successfully created in '{environmentName}'."
                : $"Parameter successfully updated in '{environmentName}'.";

            // Prepare the response model with the success message and parameter details
            var value = new ParameterResponseModel
            {
                Message = message
            };

            // Add the parameter name and value to the response
            value.Parameter[parameter.Name] = parameterValue;

            // Return 201 Created with the Location header pointing to the GET endpoint
            if (statusCode == 201)
            {
                return Created(uri: $"{HttpContext.Request.Path}", value);
            }

            // Return a ContentResult with the appropriate status code and JSON content
            return new ContentResult
            {
                StatusCode = statusCode,
                Content = JsonSerializer.Serialize(value, options: _domain.JsonOptions),
                ContentType = MediaTypeNames.Application.Json,
            };
        }
    }
}
