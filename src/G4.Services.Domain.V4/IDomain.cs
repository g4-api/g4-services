using G4.Api;
using G4.Cache;
using G4.Services.Domain.V4.Hubs;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using System.Text.Json;

namespace G4.Services.Domain.V4
{
    /// <summary>
    /// Defines the contract for the G4 domain, providing access to core services and clients.
    /// </summary>
    public interface IDomain
    {
        /// <summary>
        /// Gets or sets the cache manager used within the domain.
        /// </summary>
        CacheManager Cache { get; set; }

        /// <summary>
        /// Gets or sets the G4 client used to interact with G4 services.
        /// </summary>
        G4Client G4Client { get; set; }

        /// <summary>
        /// Gets or sets the JSON serializer options used for serialization.
        /// </summary>
        JsonSerializerOptions JsonOptions { get; set; }

        /// <summary>
        /// Gets or sets the logger used for logging within the domain.
        /// </summary>
        ILogger Logger { get; set; }

        public IHubContext<G4AutomationNotificationsHub> NotificationsHubContext { get; set; }
    }
}
