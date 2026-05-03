using G4.Abstraction.Cli;
using G4.Api;
using G4.Cache;
using G4.Models;
using G4.Services.Domain.V4.Clients;
using G4.Services.Domain.V4.Hubs;
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
        public IAspAdapter Asp { get; set; } = aspAdapter;

        /// <inheritdoc />
        public IG4Adapter G4 { get; set; } = g4Adapter;

        /// <inheritdoc />
        public IHubsAdapter Hubs { get; set; } = hubsAdapter;

        /// <inheritdoc />
        public IResourcesAdapter Resources { get; set; } = resourcesAdapter;
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
        IWebHostEnvironment environment) : IAspAdapter
    {
        /// <inheritdoc />
        public IWebHostEnvironment Environment { get; set; } = environment;

        /// <inheritdoc />
        public JsonSerializerOptions JsonOptions { get; set; } = jsonOptions;

        /// <inheritdoc />
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
        IMcpRepository mcp,
        G4Client g4,
        IOpenAiClient openAi,
        IToolsRepository tools) : IG4Adapter
    {
        /// <inheritdoc />
        public IBotsRepository Bots { get; set; } = bots;

        /// <inheritdoc />
        public CliFactory CliFactory { get; set; } = new CliFactory();


        /// <inheritdoc />
        public G4Client Client { get; set; } = g4;

        /// <inheritdoc />
        public IMcpRepository Mcp { get; set; } = mcp;

        /// <inheritdoc />
        public IOpenAiClient OpenAi { get; set; } = openAi;

        /// <inheritdoc />
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
        IHubContext<G4BotsHub> botsHubContext) : IHubsAdapter
    {
        /// <inheritdoc />
        public IHubContext<G4Hub> G4HubContext { get; set; } = g4HubContext;

        /// <inheritdoc />
        public IHubContext<G4AutomationNotificationsHub> NotificationsHubContext { get; set; } = notificationsHubContext;

        /// <inheritdoc />
        public IHubContext<G4BotsHub> BotsHubContext { get; set; } = botsHubContext;
    }

    /// <summary>
    /// Adapter class responsible for managing resources such as caching and SVG data.
    /// It provides access to the CacheManager and SvgCacheModel, allowing efficient 
    /// management of cached data and SVG resources for the G4 platform.
    /// </summary>
    internal class ResourcesAdapter(
        CacheManager cache,
        SvgCacheModel svgCache) : IResourcesAdapter
    {
        /// <inheritdoc />
        public CacheManager Cache { get; set; } = cache;

        /// <inheritdoc />
        public SvgCacheModel SvgCache { get; set; } = svgCache;
    }
}
