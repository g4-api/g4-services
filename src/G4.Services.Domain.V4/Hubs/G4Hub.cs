using G4.Models;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Hubs
{
    /// <summary>
    /// Represents the SignalR hub for G4™ services, facilitating real-time communication.
    /// </summary>
    public class G4Hub(IDomain domain) : Hub
    {
        // Domain instance for accessing core services and clients.
        private readonly IDomain _domain = domain;

        // Logger instance for logging hub activities.
        private readonly ILogger _logger = domain.Logger;

        // Handles the "SendHeartbeat" method invoked by clients. Logs the connection ID
        // and responds with a heartbeat acknowledgment.
        [HubMethodName(name: nameof(SendHeartbeat))]
        public Task SendHeartbeat()
        {
            // Log the connection ID of the client that invoked the SendHeartbeat method.
            _logger.LogInformation("Heartbeat received from Connection ID: {ConnectionId}", Context.ConnectionId);

            // Send a heartbeat acknowledgment back to the calling client.
            return Clients.Caller.SendAsync("ReceiveHeartbeat", "Heartbeat received, connection is active.");
        }

        // Retrieves the pending automation request from the QueueManager and sends it back to the caller.
        // If no pending automation is available, a corresponding message is sent instead.
        [HubMethodName(name: nameof(GetPendingAutomation))]
        public Task GetPendingAutomation()
        {
            // Log that the GetPendingAutomation method has been invoked by a client.
            _logger.LogInformation("GetPendingAutomation invoked by Connection ID: {ConnectionId}", Context.ConnectionId);

            // Retrieve the pending automation request from the QueueManager.
            var pendingAutomation = _domain.G4Client.AutomationAsync.QueueManager.GetPending();

            // Return the pending automation to the caller, or a message if none is available.
            return Clients.Caller.SendAsync("ReceivePendingAutomation", pendingAutomation);
        }

        // Receives an automation response from the hub and processes it by adding the completed automation entry.
        // Then, it sends a confirmation back to the caller.
        [HubMethodName(name: nameof(CompleteAutomation))]
        public Task CompleteAutomation(string key, G4AutomationResponseModel response)
        {
            // Add the received automation response to the client's completed automation collection.
            _domain.G4Client.AutomationAsync.AddCompletedAutomation(key, response);

            // Send a confirmation message back to the caller indicating the automation response has been received.
            return Clients.Caller.SendAsync("ConfirmAutomationResponse", key);
        }
    }
}
