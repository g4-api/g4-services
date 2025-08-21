using System;

namespace G4.Models
{
    /// <summary>
    /// Represents the configuration settings required to connect to the OpenAI API.
    /// Values are typically loaded from appsettings.json or environment variables.
    /// </summary>
    public class OpenAiSettingsModel
    {
        /// <summary>
        /// Gets or sets the OpenAI API key used for authentication.
        /// This value may be loaded from the "OpenAi:ApiKey" config section or the "OPENAI_API_KEY" environment variable.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the advanced OpenAI client options, such as endpoint, timeout,
        /// organization ID, retry policy, and telemetry-related headers.
        /// </summary>
        public OpenAiClientOptions ClientOptions { get; set; }

        public class OpenAiClientOptions
        {
            public string Endpoint { get; set; }
            public TimeSpan? NetworkTimeout { get; set; }
            public string OrganizationId { get; set; }
            public string ProjectId { get; set; }
            public OpenAiRetryPolicyOptions RetryPolicy { get; set; }
            public string UserAgentApplicationId { get; set; }
        }

        public class OpenAiRetryPolicyOptions
        {
            public double BackoffFactor { get; set; }
            public TimeSpan? Delay { get; set; } = TimeSpan.FromMilliseconds(100);
            public TimeSpan? MaxDelay { get; set; } = TimeSpan.FromSeconds(1);
            public int MaxRetries { get; set; } = 3;
        }
    }
}
