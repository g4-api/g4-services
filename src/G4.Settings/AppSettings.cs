using G4.Converters;
using G4.Models;

using Microsoft.Extensions.Configuration;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace G4.Settings
{
    /// <summary>
    /// Represents the application settings including configuration, JSON options, and LiteDB connection.
    /// </summary>
    public static class AppSettings
    {
        #region *** Constants ***
        /// <summary>
        /// The version of the API.
        /// </summary>
        public const string ApiVersion = "4";
        #endregion

        #region *** Fields    ***
        /// <summary>
        /// Gets the application configuration.
        /// </summary>
        public static readonly IConfigurationRoot Configuration = NewConfiguraion();

        /// <summary>
        /// Gets the JSON serialization options.
        /// </summary>
        public static readonly JsonSerializerOptions JsonOptions = NewJsonOptions();

        /// <summary>
        /// The URI of the login manager.
        /// </summary>
        public static readonly Uri LoginManagerUri = GetLoginManagerUri();

        /// <summary>
        /// Gets the OpenAI JSON serialization options.
        /// </summary>
        public static readonly JsonSerializerOptions OpenAiJsonOptions = NewOpenAiJsonOptions();

        /// <summary>
        /// Holds the OpenAI configuration settings loaded at application startup.
        /// This includes the API key and endpoint URL, typically sourced from appsettings or environment variables.
        /// </summary>
        public static readonly OpenAiSettingsModel OpenAi = NewOpenAiSettings();

        /// <summary>
        /// Gets the LiteDB connection.
        /// </summary>
        public static readonly StateManagerModel StateManager = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(path: "appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build()
            .GetSection("G4:StateManager")
            .Get<StateManagerModel>();
        #endregion

        #region *** Methods   ***
        // Gets the login manager URI from the application settings.
        private static Uri GetLoginManagerUri()
        {
            try
            {
                // Build the configuration from appsettings.json and environment variables
                // Retrieve the URI string from configuration section "G4:LoginManager"
                var uriString = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile(path: "appsettings.json", optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build()
                    .GetSection("G4:LoginManagerUri")
                    .Get<string>();

                // Convert the URI string to a Uri object and return
                return new Uri(uriString.TrimEnd('/') + "/api/Account/FindUser");
            }
            catch
            {
                // Return default URI if any error occurs during URI retrieval
                return default;
            }
        }

        // Creates a new instance of IConfigurationRoot by configuring it with settings from appsettings.json and environment variables.
        private static IConfigurationRoot NewConfiguraion()
        {
            // Create a new ConfigurationBuilder instance
            new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(path: "appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            // Create a new ConfigurationBuilder instance
            var configurationBuilder = new ConfigurationBuilder();

            // Set the base path for the configuration file to the current directory
            configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());

            // Add the appsettings.json file as a configuration source, if it exists (optional), without reloading it on change
            configurationBuilder.AddJsonFile(path: "appsettings.json", optional: true, reloadOnChange: false);

            // Add environment variables as a configuration source
            configurationBuilder.AddEnvironmentVariables();

            // Build and return the IConfigurationRoot instance
            return configurationBuilder.Build();
        }

        // Creates a new instance of JsonSerializerOptions with custom settings and converters.
        private static JsonSerializerOptions NewJsonOptions()
        {
            // Initialize JSON serialization options.
            var jsonOptions = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            // Add a custom exception converter
            jsonOptions.Converters.Add(new ExceptionConverter());

            // Add a custom method base converter
            jsonOptions.Converters.Add(new MethodBaseConverter());

            // Add a custom type converter
            jsonOptions.Converters.Add(new TypeConverter());

            // Add a custom DateTime converter for ISO 8601 format (yyyy-MM-ddTHH:mm:ss.ffffffK)
            jsonOptions.Converters.Add(new DateTimeIso8601Converter());

            // Return the JSON options with custom settings and converters added
            return jsonOptions;
        }

        // Creates a new instance of JsonSerializerOptions with custom settings and converters.
        private static JsonSerializerOptions NewOpenAiJsonOptions()
        {
            // Initialize JSON serialization options.
            var jsonOptions = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = false
            };

            // Add a custom exception converter
            jsonOptions.Converters.Add(new ExceptionConverter());

            // Add a custom method base converter
            jsonOptions.Converters.Add(new MethodBaseConverter());

            // Add a custom type converter
            jsonOptions.Converters.Add(new TypeConverter());

            // Add a custom DateTime converter for ISO 8601 format (yyyy-MM-ddTHH:mm:ss.ffffffK)
            jsonOptions.Converters.Add(new DateTimeIso8601Converter());

            // Return the JSON options with custom settings and converters added
            return jsonOptions;
        }

        // Loads OpenAI settings from appsettings.json and environment variables
        private static OpenAiSettingsModel NewOpenAiSettings()
        {
            try
            {
                // Build a configuration provider from appsettings.json and environment variables
                var openAiSettingsSection = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile(path: "appsettings.json", optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build()
                    .GetSection("OpenAi");

                // Bind configuration section to strongly typed settings model
                var openAiSettings = openAiSettingsSection.Get<OpenAiSettingsModel>() ?? new OpenAiSettingsModel();

                // Get the OpenAI API endpoint from env var or default
                var endpoint = GetOrDefault(
                    environmentParameter: "OPENAI_API_ENDPOINT",
                    defaultValue: openAiSettings.ClientOptions?.Endpoint);

                // Get the max retry count from env var or fallback to appsettings default
                var defaultMaxRetries = openAiSettings.ClientOptions?.RetryPolicy == default ? 3 : openAiSettings.ClientOptions.RetryPolicy.MaxRetries;
                var maxRetires = GetOrDefault(
                    environmentParameter: "OPENAI_MAX_RETRIES",
                    defaultValue: defaultMaxRetries);

                // Set the API key from environment variable or fallback to bound config
                openAiSettings.ApiKey = GetOrDefault(
                    environmentParameter: "OPENAI_API_KEY",
                    defaultValue: openAiSettings.ApiKey);

                // Apply the endpoint URI if present
                openAiSettings.ClientOptions ??= new OpenAiSettingsModel.OpenAiClientOptions();
                openAiSettings.ClientOptions.Endpoint = string.IsNullOrEmpty(endpoint)
                    ? null
                    : endpoint;

                // Set network timeout from environment or fallback to current value
                openAiSettings.ClientOptions.NetworkTimeout = GetOrDefault(
                    environmentParameter: "OPENAI_NETWORK_TIMEOUT",
                    defaultValue: openAiSettings.ClientOptions.NetworkTimeout);

                // Set organization ID from environment or fallback
                openAiSettings.ClientOptions.OrganizationId = GetOrDefault(
                    environmentParameter: "OPENAI_ORGANIZATION_ID",
                    defaultValue: openAiSettings.ClientOptions.OrganizationId);

                // Set project ID from environment or fallback
                openAiSettings.ClientOptions.ProjectId = GetOrDefault(
                    environmentParameter: "OPENAI_PROJECT_ID",
                    defaultValue: openAiSettings.ClientOptions.ProjectId);

                // Apply retry policy using resolved max retry count
                openAiSettings.ClientOptions.RetryPolicy = openAiSettings.ClientOptions.RetryPolicy == default
                    ? new() { MaxRetries = maxRetires }
                    : openAiSettings.ClientOptions.RetryPolicy;

                // Set the user-agent application ID (used for telemetry or tracking)
                openAiSettings.ClientOptions.UserAgentApplicationId = GetOrDefault(
                    environmentParameter: "OPENAI_USER_AGENT_APPLICATION_ID",
                    defaultValue: openAiSettings.ClientOptions.UserAgentApplicationId);

                return openAiSettings;
            }
            catch
            {
                // Return a default instance if anything fails (to avoid throwing during startup)
                return new();
            }
        }

        // Retrieves a value from an environment variable and converts it to the specified type <typeparamref name="T"/>.
        // If the environment variable is not found, empty, or cannot be converted, returns the provided default value.
        private static T GetOrDefault<T>(string environmentParameter, T defaultValue)
        {
            // Attempt to read the environment variable value
            var envValue = Environment.GetEnvironmentVariable(environmentParameter);

            // If the environment variable is missing or blank, use the default value
            if (string.IsNullOrWhiteSpace(envValue))
            {
                return defaultValue;
            }

            try
            {
                // Check if T is a nullable type and get the underlying type
                var underlyingType = Nullable.GetUnderlyingType(typeof(T));
                var targetType = underlyingType ?? typeof(T);

                // Special handling for booleans to support "true", "false", "1", and "0"
                if (targetType == typeof(bool))
                {
                    if (bool.TryParse(envValue, out bool boolResult))
                    {
                        return (T)(object)boolResult;
                    }

                    // Support numeric boolean representation
                    if (envValue.Trim() == "1") return (T)(object)true;
                    if (envValue.Trim() == "0") return (T)(object)false;
                }

                // Attempt to convert the string value to the target type using system conversion
                return (T)Convert.ChangeType(envValue.Trim(), targetType);
            }
            catch
            {
                // If conversion fails (e.g., invalid format), fall back to the default value
                return defaultValue;
            }
        }
        #endregion
    }
}
