using Swashbuckle.AspNetCore.Annotations;

namespace G4.Models.Schema
{
    /// <summary>
    /// Input schema for resolving a locator expression using the G4 engine.
    /// Encapsulates the driver session identifier, intent, and authorization token
    /// required to determine a locator for UI elements or DOM nodes.
    /// </summary>
    [SwaggerSchema(description: "Schema for resolving a locator expression using the G4 engine.")]
    public class ResolveLocatorInputSchema
    {
        /// <summary>
        /// Gets or sets the identifier of the active driver/browser session.
        /// This tells the G4 engine which session’s DOM should be targeted.
        /// </summary>
        [SwaggerSchema(description: "The driver session identifier specifying which active session’s " +
            "DOM should be targeted.")]
        public string DriverSession { get; set; }

        /// <summary>
        /// The unique chat identifier associated with this request.
        /// Used to correlate tool lookups with a specific chat session.
        /// </summary>
        [SwaggerSchema(description: "The unique chat identifier used to correlate the request " +
            "with a specific session.")]
        public object Id { get; set; }

        /// <summary>
        /// Gets or sets the intent that describes the element or action 
        /// for which a locator should be resolved.
        /// This may be used for semantic or vector-based lookup when an exact match is not known.
        /// </summary>
        [SwaggerSchema(description: "The intent describing the target element or action. " +
            "Used for vector-based lookup if an exact locator is not available.")]
        public string Intent { get; set; }

        /// <summary>
        /// The OpenAI API key used for authentication. Optional.
        /// </summary>
        [SwaggerSchema(description: "Optional: OpenAI API key for authentication when " +
            "using OpenAI-backed resolution.", Nullable = true)]
        public string OpenaiApiKey { get; set; }

        /// <summary>
        /// The OpenAI model identifier (e.g., gpt-4o, gpt-4.1-mini). Optional.
        /// </summary>
        [SwaggerSchema(description: "Optional: OpenAI model identifier " +
            "(e.g., 'gpt-4o', 'gpt-4.1-mini').", Nullable = true)]
        public string OpenaiModel { get; set; }

        /// <summary>
        /// The OpenAI API base URI (default or custom endpoint). Optional.
        /// </summary>
        [SwaggerSchema(description: "Optional: OpenAI API base URI (use default or a " +
            "custom endpoint).", Nullable = true)]
        public string OpenaiUri { get; set; }

        /// <summary>
        /// Gets or sets the authorization token required by the G4 engine
        /// to authorize and process the locator resolution request.
        /// </summary>
        [SwaggerSchema(description: "The authorization token used by the G4 engine to " +
            "process the locator resolution request.")]
        public string Token { get; set; }
    }
}
