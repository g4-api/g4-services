using Swashbuckle.AspNetCore.Annotations;

namespace G4.Services.Domain.V4.Models.Schema
{
    /// <summary>
    /// Input schema for starting the execution of a collection of rules within an active G4 driver session.
    /// Requires a driver session identifier, the ordered rule definitions to execute, and an authorization token.
    /// </summary>
    [SwaggerSchema(description: "Schema for starting the execution of a collection of rules within an active G4 driver session.")]
    public class SendRulesInputSchema
    {
        /// <summary>
        /// Gets or sets the driver session identifier.
        /// Specifies the active session in which the rules should be executed.
        /// </summary>
        [SwaggerSchema(description: "The unique session ID associated with the current browser session. " +
            "This ID is used to retrieve the appropriate browser driver for interacting with " +
            "the session and performing automation tasks.")]
        public string DriverSession { get; set; }

        /// <summary>
        /// Gets or sets the user intent or natural-language request
        /// that describes the purpose of the rules being sent.
        /// </summary>
        [SwaggerSchema(description: "The user intent or natural-language request that describes the purpose of the rules being sent.")]
        public IntentModel Intent { get; set; }

        /// <summary>
        /// Gets or sets the ordered collection of rule definitions to execute.
        /// This may be a structured array representing the automation rule logic.
        /// </summary>
        [SwaggerSchema(description: "The ordered collection of G4 rules to be executed, including their parameters and configuration.")]
        public object Rules { get; set; }

        /// <summary>
        /// Gets or sets the authorization token required by the G4 engine
        /// to validate and execute the rule request.
        /// </summary>
        [SwaggerSchema(description: "The G4 Authentication token used to authenticate the session initiation process. " +
            "This is required to authorize the session creation.")]
        public string Token { get; set; }
    }
}
