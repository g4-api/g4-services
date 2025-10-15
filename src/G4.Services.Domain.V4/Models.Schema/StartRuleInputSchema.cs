using Swashbuckle.AspNetCore.Annotations;

using System.Text.Json.Serialization;

namespace G4.Services.Domain.V4.Models.Schema
{
    /// <summary>
    /// Input schema for starting a rule execution within an active G4 driver session.  
    /// Requires a driver session identifier, the rule definition to execute, and an authorization token.
    /// </summary>
    [SwaggerSchema(description: "Schema for starting a rule execution within an active G4 driver session.")]
    public class StartRuleInputSchema
    {
        /// <summary>
        /// Gets or sets the driver session identifier.  
        /// Specifies the active session in which the rule should be executed.
        /// </summary>
        [JsonPropertyName("driver_session")]
        [SwaggerSchema(description: "The unique session ID associated with the current browser session. " +
            "This ID is used to retrieve the appropriate browser driver for interacting with " +
            "the session and performing automation tasks.")]
        public string DriverSession { get; set; }

        /// <summary>
        /// Gets or sets the rule definition to execute.  
        /// This may be a structured object representing the automation rule logic.
        /// </summary>
        [SwaggerSchema(description: "The G4 Authentication token used to authenticate the session initiation process. " +
            "This is required to authorize the session creation.")]
        public object Rule { get; set; }

        /// <summary>
        /// Gets or sets the authorization token required by the G4 engine  
        /// to validate and execute the rule request.
        /// </summary>
        [SwaggerSchema(description: "The G4 rule to be executed, including its parameters and configuration.")]
        public string Token { get; set; }
    }
}
