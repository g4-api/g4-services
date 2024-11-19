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
    public readonly struct AppSettings
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
        #endregion
    }
}
