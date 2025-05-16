using G4.Api;
using G4.Cache;
using G4.Services.Domain.V4.Hubs;
using G4.Services.Domain.V4.Repositories;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using System.Text.Json;

namespace G4.Services.Domain.V4
{
    /// <summary>
    /// Represents the core domain services and state for the G4 application,
    /// including SignalR hub contexts, caching, serialization settings, and logging.
    /// </summary>
    public interface IDomain
    {
        /// <summary>
        /// Gets or sets the SignalR hub context for communicating with bot-specific connected clients.
        /// </summary>
        IHubContext<G4BotsHub> BotsHubContext { get; set; }

        /// <summary>
        /// Gets or sets the cache manager responsible for storing and retrieving
        /// domain-level cache entries to improve performance and reduce external calls.
        /// </summary>
        CacheManager Cache { get; set; }

        IBotsRepository Bots { get; set; }

        /// <summary>
        /// Gets or sets the G4 API client used to interact with external G4 services
        /// for operations such as job dispatching and status retrieval.
        /// </summary>
        G4Client G4Client { get; set; }

        /// <summary>
        /// Gets or sets the SignalR hub context for general G4 notifications
        /// and messaging outside the bot-specific hub.
        /// </summary>
        IHubContext<G4Hub> G4HubContext { get; set; }

        /// <summary>
        /// Gets or sets the JSON serializer options that control serialization behavior,
        /// including converters, naming policies, and formatting rules.
        /// </summary>
        JsonSerializerOptions JsonOptions { get; set; }

        /// <summary>
        /// Gets or sets the logger instance for recording domain events,
        /// diagnostics, and error information to the configured logging provider.
        /// </summary>
        ILogger Logger { get; set; }

        /// <summary>
        /// Gets or sets the SignalR hub context for broadcasting automation
        /// notifications and updates to subscribed clients.
        /// </summary>
        IHubContext<G4AutomationNotificationsHub> NotificationsHubContext { get; set; }
    }
}
