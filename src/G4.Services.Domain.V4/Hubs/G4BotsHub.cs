using G4.Extensions;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using System;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Hubs
{
    /// <summary>
    /// Represents the SignalR hub for G4™ bots, facilitating real-time communication.
    /// </summary>
    public class G4BotsHub(IDomain domain) : Hub
    {
        private readonly IDomain _domain = domain;
        private readonly ILogger _logger = domain.Logger;

        /// <inheritdoc />
        public override async Task OnConnectedAsync()
        {
            // Create a new bot model for this connection
            var connectedBot = new G4Domain.ConnectedBotModel
            {
                Id = Context.ConnectionId,        // Unique connection ID assigned by SignalR
                Name = string.Empty,              // Will be provided later via RegisterBot
                Machine = string.Empty,           // Will be provided later via RegisterBot
                Type = string.Empty,              // Will be provided later via RegisterBot
                Status = "Connected",             // Set initial status to "Connected"
                CreatedOn = DateTime.UtcNow,      // Store the creation timestamp in UTC
                LastModifiedOn = DateTime.UtcNow  // Also initialize last modified timestamp
            };

            // Add this bot to the shared dictionary of connected bots
            _domain.ConnectedBots.TryAdd(Context.ConnectionId, connectedBot);

            // Log the new connection for monitoring/debugging purposes
            _logger.LogInformation("Bot connected: {ConnectionId}", Context.ConnectionId);

            // Continue with base hub connection logic
            await base.OnConnectedAsync();
        }

        /// <inheritdoc />
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            // Attempt to remove the bot entry from the connected bots dictionary
            _domain.ConnectedBots.TryRemove(Context.ConnectionId, out _);

            // Log the disconnection event
            _logger.LogInformation("Bot disconnected: {ConnectionId}", Context.ConnectionId);

            // Proceed with the base class disconnection logic
            await base.OnDisconnectedAsync(exception);
        }

        // Handles heartbeat pings from connected clients.
        // Logs the activity and responds with an acknowledgment to confirm active connection.
        [HubMethodName(nameof(SendHeartbeat))]
        public async Task SendHeartbeat()
        {
            // Log the incoming heartbeat for monitoring
            _logger.LogInformation("Heartbeat received from Connection ID: {ConnectionId}", Context.ConnectionId);

            // Send acknowledgment back to the calling client
            await Clients.Caller.SendAsync("ReceiveHeartbeat", "Heartbeat received, connection is active.");
        }

        // Handles initial bot registration after connection.
        // Updates metadata such as name, type, machine, and description from the provided client data.
        [HubMethodName(nameof(RegisterBot))]
        public async Task RegisterBot(G4Domain.ConnectedBotModel registrationRequest)
        {
            // Attempt to retrieve the bot associated with the current connection ID
            var isConnected = _domain.ConnectedBots.TryGetValue(Context.ConnectionId, out var connectedBot);

            // If the bot is not found in the registry, exit early
            if (!isConnected)
            {
                return;
            }

            // Update the bot's details using the registration request
            // If values are null, keep existing values unchanged
            connectedBot.Name = registrationRequest.Name ?? connectedBot.Name;
            connectedBot.Type = registrationRequest.Type ?? connectedBot.Type;
            connectedBot.Machine = registrationRequest.Machine ?? connectedBot.Machine;

            // Set the bot's status to "Ready" after registration
            connectedBot.Status = "Ready";

            // Update the last modified timestamp to reflect this update
            connectedBot.LastModifiedOn = DateTime.UtcNow;

            // Log the registration details
            _logger.LogInformation(
                message: "Bot registered: {ConnectionId} Name:{Name} Type:{Type}",
                Context.ConnectionId, connectedBot.Name, connectedBot.Type
            );

            // Send a success response back to the caller
            await Clients.Caller.SendAsync("ReceiveRegisterBot", new
            {
                StatusCode = 200,
                Message = $"Bot '{connectedBot.Name}' registered successfully."
            });
        }

        // Updates the status of the connected bot.
        // This method is typically used to reflect real-time state changes (e.g., Online, Idle, Busy).
        [HubMethodName(nameof(UpdateBot))]
        public async Task UpdateBot(G4Domain.ConnectedBotModel updateRequest)
        {
            // Attempt to retrieve the bot associated with the current connection
            var isConnected = _domain.ConnectedBots.TryGetValue(Context.ConnectionId, out var connectedBot);

            if (!isConnected)
            {
                // Connection not found in the connected bot registry — exit early
                return;
            }

            // Update the bot's operational status
            connectedBot.Status = updateRequest.Status;

            // Refresh the last modified timestamp
            connectedBot.LastModifiedOn = DateTime.UtcNow;

            // Log the update for auditing or monitoring purposes
            _logger.LogInformation(
                message: "Bot updated: {ConnectionId}, Status:{Status}",
                Context.ConnectionId,
                connectedBot.Status
            );

            // Notify the caller with the updated bot model
            await Clients.Caller.SendAsync("ReceiveBotUpdated", connectedBot);
        }
    }
}
