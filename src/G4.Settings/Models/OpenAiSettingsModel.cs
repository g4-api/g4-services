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

        /// <summary>
        /// Configuration options for the OpenAI client used within the G4 ecosystem.
        /// Defines the API endpoint, timeout behavior, organization/project scoping,
        /// retry policy configuration, and user-agent identification.
        /// Typically bound from configuration (appsettings.json or environment variables)
        /// and injected via IOptions<OpenAiClientOptions>.
        /// </summary>
        public class OpenAiClientOptions
        {
            /// <summary>
            /// Base URL of the OpenAI-compatible API endpoint (e.g., https://api.openai.com/v1
            /// or a local proxy such as http://localhost:8080/v1). Must be a fully qualified URI.
            /// </summary>
            public string Endpoint { get; set; }

            /// <summary>
            /// Optional network timeout for HTTP requests. If null, the default HttpClient
            /// timeout is used. Recommended to configure explicitly in production
            /// to prevent hanging requests.
            /// </summary>
            public TimeSpan? NetworkTimeout { get; set; }

            /// <summary>
            /// Optional OpenAI organization identifier. When provided, it is typically
            /// sent using the "OpenAI-Organization" header to scope requests to a specific organization.
            /// </summary>
            public string OrganizationId { get; set; }

            /// <summary>
            /// Optional OpenAI project identifier. When provided, it is typically sent
            /// using the "OpenAI-Project" header to scope API usage to a specific project.
            /// </summary>
            public string ProjectId { get; set; }

            /// <summary>
            /// Retry policy configuration used to handle transient failures such as
            /// HTTP 429 (rate limiting), HTTP 5xx errors, and network-level issues.
            /// If null, default or no retry behavior will apply.
            /// </summary>
            public OpenAiRetryPolicyOptions RetryPolicy { get; set; }

            /// <summary>
            /// Optional application identifier included in the User-Agent header
            /// (e.g., "G4-Ronin/1.0.0") to improve observability and API diagnostics.
            /// </summary>
            public string UserAgentApplicationId { get; set; }
        }

        /// <summary>
        /// Defines retry policy settings for handling transient OpenAI API failures,
        /// including exponential backoff behavior, delay limits, and maximum retry attempts.
        /// Typically used to handle HTTP 429 (rate limiting), HTTP 5xx errors,
        /// and temporary network issues.
        /// </summary>
        public class OpenAiRetryPolicyOptions
        {
            /// <summary>
            /// Multiplier applied to the delay between retries to implement exponential backoff.
            /// For example, a value of 2.0 doubles the delay after each retry attempt.
            /// </summary>
            public double BackoffFactor { get; set; }

            /// <summary>
            /// Initial delay before the first retry attempt.
            /// Defaults to 100 milliseconds if not explicitly configured.
            /// </summary>
            public TimeSpan? Delay { get; set; } = TimeSpan.FromMilliseconds(100);

            /// <summary>
            /// Maximum allowed delay between retries.
            /// Ensures exponential backoff does not grow beyond this limit.
            /// Defaults to 1 second.
            /// </summary>
            public TimeSpan? MaxDelay { get; set; } = TimeSpan.FromSeconds(1);

            /// <summary>
            /// Maximum number of retry attempts before failing the request.
            /// Defaults to 3 retries.
            /// </summary>
            public int MaxRetries { get; set; } = 3;
        }
    }
}