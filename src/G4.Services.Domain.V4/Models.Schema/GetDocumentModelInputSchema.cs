using Swashbuckle.AspNetCore.Annotations;

using System.Text.Json.Serialization;

namespace G4.Services.Domain.V4.Models.Schema
{
    /// <summary>
    /// Input schema for retrieving the document model (DOM) of the current active session
    /// using the G4 engine.  
    /// Provides the driver session identifier and the token required for authorization.
    /// </summary>
    [SwaggerSchema(description: "Input schema for retrieving the current DOM (document model) of " +
        "an active session using the G4 engine.")]
    public class GetDocumentModelInputSchema
    {
        /// <summary>
        /// Gets or sets the driver session identifier.
        /// Used to specify which active session's DOM should be retrieved.
        /// </summary>
        [JsonPropertyName("driver_session")]
        [SwaggerSchema(description: "The driver session identifier that indicates which active " +
            "session's DOM should be retrieved.")]
        public string DriverSession { get; set; }

        /// <summary>
        /// The unique chat identifier associated with this request.
        /// Used to correlate tool lookups with a specific chat session.
        /// </summary>
        [SwaggerSchema(description: "The unique chat identifier used to correlate the request with a specific session.")]
        public object Id { get; set; }

        /// <summary>
        /// Gets or sets the token required to authorize the request
        /// when using the G4 engine to extract the DOM of the active session.
        /// </summary>
        [SwaggerSchema(description: "The authorization token used by the G4 engine to retrieve " +
            "the DOM of the active session.")]
        public string Token { get; set; }
    }
}
