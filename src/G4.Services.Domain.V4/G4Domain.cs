using G4.Api;
using G4.Cache;
using G4.Services.Domain.V4.Clients;
using G4.Services.Domain.V4.Hubs;
using G4.Services.Domain.V4.Models;
using G4.Services.Domain.V4.Repositories;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using System.Text.Json;

namespace G4.Services.Domain.V4
{
    /// <summary>
    /// Represents the G4 domain, providing access to core services and clients.
    /// </summary>
    internal class G4Domain(
        AspAdapter aspAdapter,
        G4Adapter g4Adapter,
        HubsAdapter hubsAdapter,
        ResourcesAdapter resourcesAdapter) : IDomain
    {
        #region *** Properties   ***
        /// <inheritdoc />
        public IBotsRepository Bots { get; set; } = g4Adapter.Bots;

        /// <inheritdoc />
        public IHubContext<G4BotsHub> BotsHubContext { get; set; } = hubsAdapter.BotsHubContext;

        /// <inheritdoc />
        public CacheManager Cache { get; set; } = resourcesAdapter.Cache;

        /// <inheritdoc />
        public ICopilotRepository Copilot { get; set; } = g4Adapter.Copilot;

        /// <inheritdoc />
        public IWebHostEnvironment Environment { get; set; } = aspAdapter.Environment;

        /// <inheritdoc />
        public G4Client G4 { get; set; } = g4Adapter.G4;

        /// <inheritdoc />
        public IHubContext<G4Hub> G4HubContext { get; set; } = hubsAdapter.G4HubContext;

        /// <inheritdoc />
        public JsonSerializerOptions JsonOptions { get; set; } = aspAdapter.JsonOptions;

        /// <inheritdoc />
        public ILogger Logger { get; set; } = aspAdapter.Logger;

        /// <inheritdoc />
        public IHubContext<G4AutomationNotificationsHub> NotificationsHubContext { get; set; } = hubsAdapter.NotificationsHubContext;

        /// <inheritdoc />
        public IOpenAiClient OpenAi { get; set; } = g4Adapter.OpenAi;

        /// <inheritdoc />
        public SvgCacheModel SvgCache { get; set; } = resourcesAdapter.SvgCache;

        /// <inheritdoc />
        public IToolsRepository Tools { get; set; } = g4Adapter.Tools;
        #endregion
    }

    /// <summary>
    /// Adapter class responsible for managing essential services related to the ASP.NET Core application,
    /// including JSON serialization options, logging, and the web host environment.
    /// This class centralizes the access to these services for use in different parts of the application.
    /// </summary>
    internal class AspAdapter(
        JsonSerializerOptions jsonOptions,
        ILogger logger,
        IWebHostEnvironment environment)
    {
        /// <summary>
        /// The environment settings for the current ASP.NET Core web host.
        /// Provides information about the hosting environment, such as whether the application
        /// is running in development, staging, or production.
        /// </summary>
        public IWebHostEnvironment Environment { get; set; } = environment;

        /// <summary>
        /// The JSON serializer settings that are used for serializing and deserializing objects.
        /// This is useful for customizing JSON handling (e.g., formatting, converters).
        /// </summary>
        public JsonSerializerOptions JsonOptions { get; set; } = jsonOptions;

        /// <summary>
        /// The logger instance used for logging events in the application.
        /// This is used for capturing and outputting logs for debugging and monitoring.
        /// </summary>
        public ILogger Logger { get; set; } = logger;
    }

    /// <summary>
    /// Adapter class that holds references to various repositories and services needed
    /// for interacting with the G4 platform. It serves as a central point for accessing
    /// core functionalities such as bot management, copilot operations, G4 client interactions, 
    /// OpenAI integration, and tools management.
    /// </summary>
    internal class G4Adapter(
        IBotsRepository bots,
        ICopilotRepository copilot,
        G4Client g4,
        IOpenAiClient openAi,
        IToolsRepository tools)
    {
        /// <summary>
        /// Repository for managing bot-related data and operations.
        /// Provides access to bot creation, retrieval, updates, and deletions.
        /// </summary>
        public IBotsRepository Bots { get; set; } = bots;

        /// <summary>
        /// Repository for managing copilot-related data and interactions.
        /// Allows handling copilot-related functionalities like training, interaction, and state management.
        /// </summary>
        public ICopilotRepository Copilot { get; set; } = copilot;

        /// <summary>
        /// The G4 client instance used for interacting with the G4 platform.
        /// Facilitates communication with G4-related services, endpoints, and resources.
        /// </summary>
        public G4Client G4 { get; set; } = g4;

        /// <summary>
        /// Client for interacting with OpenAI services.
        /// Provides methods for leveraging OpenAI's language models and other AI capabilities.
        /// </summary>
        public IOpenAiClient OpenAi { get; set; } = openAi;

        /// <summary>
        /// Repository for managing tool-related operations within the G4 platform.
        /// Handles access to tools, configurations, and associated data for automation and other tasks.
        /// </summary>
        public IToolsRepository Tools { get; set; } = tools;
    }

    /// <summary>
    /// Adapter class that holds the contexts for multiple SignalR hubs.
    /// It is responsible for providing access to the various hubs needed 
    /// for real-time communication across different areas of the G4 system.
    /// </summary>
    internal class HubsAdapter(
        IHubContext<G4Hub> g4HubContext,
        IHubContext<G4AutomationNotificationsHub> notificationsHubContext,
        IHubContext<G4BotsHub> botsHubContext)
    {
        /// <summary>
        /// The SignalR hub context for G4Hub, which handles general real-time communication
        /// within the G4 platform.
        /// </summary>
        public IHubContext<G4Hub> G4HubContext { get; set; } = g4HubContext;

        /// <summary>
        /// The SignalR hub context for G4AutomationNotificationsHub, responsible for sending 
        /// notifications related to automation tasks within the G4 platform.
        /// </summary>
        public IHubContext<G4AutomationNotificationsHub> NotificationsHubContext { get; set; } = notificationsHubContext;

        /// <summary>
        /// The SignalR hub context for G4BotsHub, used for real-time communication with bots
        /// within the G4 ecosystem.
        /// </summary>
        public IHubContext<G4BotsHub> BotsHubContext { get; set; } = botsHubContext;
    }

    /// <summary>
    /// Adapter class responsible for managing resources such as caching and SVG data.
    /// It provides access to the CacheManager and SvgCacheModel, allowing efficient 
    /// management of cached data and SVG resources for the G4 platform.
    /// </summary>
    internal class ResourcesAdapter(
        CacheManager cache,
        SvgCacheModel svgCache)
    {
        /// <summary>
        /// The CacheManager instance that handles caching operations within the application.
        /// This is used to store and retrieve data that needs to be cached for quick access.
        /// </summary>
        public CacheManager Cache { get; set; } = cache;

        /// <summary>
        /// The SvgCacheModel instance that holds cached SVG data, where each SVG file's
        /// content is stored with the file name (without extension) as the key.
        /// </summary>
        public SvgCacheModel SvgCache { get; set; } = svgCache;
    }
}
