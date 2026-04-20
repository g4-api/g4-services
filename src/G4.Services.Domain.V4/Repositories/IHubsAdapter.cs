using G4.Services.Domain.V4.Hubs;

using Microsoft.AspNetCore.SignalR;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Represents an adapter for managing SignalR hub contexts within the G4 platform.
    /// This interface provides properties for accessing the hub contexts of various SignalR
    /// hubs used in the G4 ecosystem, such as G4Hub, G4AutomationNotificationsHub, and G4BotsHub.
    /// Implementing this interface allows for centralized management and injection of hub
    /// contexts across the application, facilitating real-time communication and notifications.
    /// </summary>
    public interface IHubsAdapter
    {
        /// <summary>
        /// The SignalR hub context for G4Hub, which handles general real-time communication
        /// within the G4 platform.
        /// </summary>
        IHubContext<G4Hub> G4HubContext { get; set; }

        /// <summary>
        /// The SignalR hub context for G4AutomationNotificationsHub, responsible for sending 
        /// notifications related to automation tasks within the G4 platform.
        /// </summary>
        IHubContext<G4AutomationNotificationsHub> NotificationsHubContext { get; set; }

        /// <summary>
        /// The SignalR hub context for G4BotsHub, used for real-time communication with bots
        /// within the G4 ecosystem.
        /// </summary>
        IHubContext<G4BotsHub> BotsHubContext { get; set; }
    }
}