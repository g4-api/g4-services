using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

using System.Text.Json;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Represents an adapter for ASP.NET Core web hosting environment, JSON serialization settings, and logging.
    /// </summary>
    public interface IAspAdapter
    {
        /// <summary>
        /// The environment settings for the current ASP.NET Core web host.
        /// Provides information about the hosting environment, such as whether the application
        /// is running in development, staging, or production.
        /// </summary>
        IWebHostEnvironment Environment { get; set; }

        /// <summary>
        /// The JSON serializer settings that are used for serializing and deserializing objects.
        /// This is useful for customizing JSON handling (e.g., formatting, converters).
        /// </summary>
        JsonSerializerOptions JsonOptions { get; set; }

        /// <summary>
        /// The logger instance used for logging events in the application.
        /// This is used for capturing and outputting logs for debugging and monitoring.
        /// </summary>
        ILogger Logger { get; set; }
    }
}