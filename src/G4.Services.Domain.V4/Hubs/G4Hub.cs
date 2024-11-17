using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Hubs
{
    /// <summary>
    /// Represents the SignalR hub for G4™ services, facilitating real-time communication.
    /// </summary>
    /// <param name="logger">The logger to be used for logging hub activities.</param>
    public class G4Hub(ILogger logger) : Hub
    {
        // Logger instance for logging hub activities.
        private readonly ILogger _logger = logger;

        // Handles the "SendHeartbeat" method invoked by clients. Logs the connection ID
        // and responds with a heartbeat acknowledgment.
        [HubMethodName("SendHeartbeat")]
        public Task SendHeartbeat()
        {
            // Log the connection ID of the client that invoked the SendHeartbeat method.
            _logger.LogInformation("Heartbeat received from Connection ID: {ConnectionId}", Context.ConnectionId);

            // Send a heartbeat acknowledgment back to the calling client.
            return Clients.Caller.SendAsync("SendHeartbeat", "Heartbeat received, connection is active.");
        }
    }
}
