using G4.Credentials.Models;
using G4.Models;
using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Text.Json;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/[controller]")]
    [SwaggerTag(description: "Provides endpoints to create OAuth credentials for a " +
        "specific provider and to persist credentials after the OAuth callback completes.")]
    [ApiExplorerSettings(GroupName = "G4 Hub")]
    public class CredentialsController(IDomain domain) : ControllerBase
    {
        // The domain service instance is injected into the controller.
        private readonly IDomain _domain = domain;

        [HttpGet]
        [SwaggerOperation(
            summary: "Get all credentials",
            description: "Returns all saved credential records.",
            Tags = ["Credentials"])]
        [Produces(MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Credentials returned successfully.", type: typeof(object), contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult GetCredentials()
        {
            // Fetch all saved credentials from the domain layer.
            var credentials = _domain.G4.Client.Credentials.GetCredentials();

            // Return the credentials as a JSON response.
            return Ok(credentials);
        }

        [HttpGet]
        [Route("{idOrName}")]
        [SwaggerOperation(
            summary: "Get a credential by id or name",
            description: "Returns a single credential record matching the provided id or name.",
            Tags = ["Credentials"])]
        [Produces(MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Credential returned successfully.", type: typeof(object), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Credential was not found.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult GetCredential(
            [FromRoute][SwaggerParameter(description: "Credential id or credential name.", Required = true)] string idOrName)
        {
            // Lookup the credential using either its id or its name.
            var credential = _domain.G4.Client.Credentials.GetCredentials(idOrName);

            // If no credential exists for the provided key, return a structured 404 error payload.
            if (credential == null)
            {
                return NotFound(new GenericErrorModel(HttpContext)
                    .AddError(name: "CredentialNotFound", value: $"The credential with the ID or name '{idOrName}' was not found."));
            }

            // Return the credential as a JSON response.
            return Ok(credential);
        }

        [HttpPost("oauth/{provider}")]
        [SwaggerOperation(
            summary: "Create OAuth credentials for a provider",
            description: "Creates a new OAuth credential record for the specified provider and returns consent information (if required), scope, and configured domains.",
            Tags = ["Credentials"])]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status200OK, description: "OAuth credentials were created successfully and consent details were returned.", type: typeof(object), contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult NewOAuthCredentials(
            [FromRoute, Required][SwaggerParameter(description: "OAuth provider name (e.g., google, azure, github).", Required = true)] string provider,
            [FromBody][SwaggerParameter(description: "OAuth credentials payload for the provider.", Required = true)] OAuthCredentialModel oauth)
        {
            // Normalize the provider into the model so the domain layer can process provider-specific logic.
            oauth.Provider = provider;

            // Create the credentials record and calculate whether consent is required for this provider/settings.
            var oauthResponse = _domain.G4.Client.Credentials.NewCredentials(oauth);

            // Shape the response payload to include only the fields the client needs for the next step.
            var content = new
            {
                oauthResponse.ClientId,
                oauthResponse.ConsentUrl,
                oauthResponse.Domains,
                oauthResponse.RequiresConsent,
                oauthResponse.Scope
            };

            // Return JSON explicitly using the domain serializer options for consistent formatting.
            return new ContentResult
            {
                Content = JsonSerializer.Serialize(content, _domain.Asp.JsonOptions),
                ContentType = MediaTypeNames.Application.Json,
                StatusCode = StatusCodes.Status200OK
            };
        }

        [HttpDelete]
        [Route("{idOrName}")]
        [SwaggerOperation(
            summary: "Remove a credential by id or name",
            description: "Removes a single credential record matching the provided id or name.",
            Tags = ["Credentials"])]
        [Produces(MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Credential removed successfully.", type: typeof(object), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Credential was not found.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult RemoveCredential(
            [FromRoute][SwaggerParameter(description: "Credential id or credential name.", Required = true)] string idOrName)
        {
            // Attempt to remove the credential using either its id or its name.
            var removedCount = _domain.G4.Client.Credentials.RemoveCredentials(idOrName);

            // If no credential exists for the provided key, return a structured 404 error payload.
            if (removedCount == 0)
            {
                return NotFound(new GenericErrorModel(HttpContext)
                    .AddError(name: "CredentialNotFound", value: $"The credential with the ID or name '{idOrName}' was not found."));
            }

            // Successful deletion — return HTTP 204 with no body.
            return NoContent();
        }

        [HttpGet("callback")]
        [SwaggerOperation(
            summary: "Persist OAuth credentials after provider callback",
            description: "Consumes the OAuth authorization code (and state) returned by the provider callback, exchanges it for tokens, and persists the resulting credentials.",
            Tags = ["Credentials"])]
        [Produces(MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Credentials were saved successfully.", type: typeof(object), contentTypes: MediaTypeNames.Application.Json)]
        public IActionResult SaveCredentials(
            [FromQuery][SwaggerParameter(description: "OAuth authorization code returned by the provider.", Required = true)] string code,
            [FromQuery][SwaggerParameter(description: "Opaque state value returned by the provider (used to correlate the flow).", Required = false)] string state)
        {
            // Persist the credentials by exchanging the authorization code for tokens (refresh/access) in the domain layer.
            var oauth = _domain.G4.Client.Credentials.SaveCredentials(new()
            {
                Code = code,
                State = state
            });

            // Return a minimal success payload without exposing the refresh token itself.
            return Ok(new
            {
                oauth.Id,
                oauth.ExpiresAt,
                HasRefreshToken = !string.IsNullOrWhiteSpace(oauth.RefreshToken)
            });
        }
    }
}
