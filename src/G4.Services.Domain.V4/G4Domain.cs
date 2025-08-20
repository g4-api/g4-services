using G4.Api;
using G4.Cache;
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
        CacheManager cache,
        G4Client g4,
        JsonSerializerOptions jsonOptions,
        IBotsRepository connectedBots,
        ICopilotRepository copilot,
        IHubContext<G4AutomationNotificationsHub> notificationsHubContext,
        IHubContext<G4Hub> g4HubContext,
        IHubContext<G4BotsHub> botsHubContext,
        ILogger logger,
        IOpenAiClient openAi,
        IWebHostEnvironment environment) : IDomain
    {
        #region *** Properties   ***
        /// <inheritdoc />
        public IBotsRepository Bots { get; set; } = connectedBots;

        /// <inheritdoc />
        public IHubContext<G4BotsHub> BotsHubContext { get; set; } = botsHubContext;

        /// <inheritdoc />
        public CacheManager Cache { get; set; } = cache;

        /// <inheritdoc />
        public ICopilotRepository Copilot { get; set; } = copilot;

        /// <inheritdoc />
        public IWebHostEnvironment Environment { get; set; } = environment;

        /// <inheritdoc />
        public G4Client G4 { get; set; } = g4;

        /// <inheritdoc />
        public IHubContext<G4Hub> G4HubContext { get; set; } = g4HubContext;

        /// <inheritdoc />
        public JsonSerializerOptions JsonOptions { get; set; } = jsonOptions;

        /// <inheritdoc />
        public ILogger Logger { get; set; } = logger;

        /// <inheritdoc />
        public IHubContext<G4AutomationNotificationsHub> NotificationsHubContext { get; set; } = notificationsHubContext;

        /// <inheritdoc />
        public IOpenAiClient OpenAi { get; set; } = openAi;
        #endregion
    }
}
