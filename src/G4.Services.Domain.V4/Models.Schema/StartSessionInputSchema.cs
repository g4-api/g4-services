using Swashbuckle.AspNetCore.Annotations;

using System.Text.Json.Serialization;

namespace G4.Services.Domain.V4.Models.Schema
{
    /// <summary>
    /// Input schema for starting a new driver session using the G4 engine.
    /// Defines required driver settings and optional session configuration.
    /// </summary>
    [SwaggerSchema(description: "Schema for starting a new driver session using the G4 engine.")]
    public class StartSessionInputSchema
    {
        /// <summary>
        /// Gets or sets the capabilities that must always be matched
        /// when creating the session. Optional.
        /// </summary>
        [JsonPropertyName("always_match")]
        [SwaggerSchema(description: "Optional: Capabilities that must always be matched when " +
            "creating the session.", Nullable = true)]
        public object AlwaysMatch { get; set; }

        /// <summary>
        /// Gets or sets the driver name or type (e.g., ChromeDriver, GeckoDriver).
        /// This field is required.
        /// </summary>
        [JsonPropertyName("driver")]
        [SwaggerSchema(description: "Required: The driver name or type (e.g., ChromeDriver, FirefoxDriver).")]
        public string Driver { get; set; }

        /// <summary>
        /// Gets or sets the path or URL to the driver binaries.
        /// This field is required.
        /// </summary>
        [JsonPropertyName("driver_binaries")]
        [SwaggerSchema(description: "Required: The path or URL to the driver binaries.")]
        public string DriverBinaries { get; set; }

        /// <summary>
        /// Gets or sets an array of first-match capability sets
        /// used to negotiate session configuration. Optional.
        /// </summary>
        [JsonPropertyName("first_match")]
        [SwaggerSchema(description: "Optional: Array of first-match capability sets used to " +
            "negotiate session configuration.", Nullable = true)]
        public object[] FirstMatch { get; set; }

        /// <summary>
        /// Gets or sets the authorization token required by the G4 engine
        /// to start a new session. This field is required.
        /// </summary>
        [JsonPropertyName("token")]
        [SwaggerSchema(description: "Required: The authorization token used by the G4 engine to " +
            "start a new session.")]
        public string Token { get; set; }
    }
}
