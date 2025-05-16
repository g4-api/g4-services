using G4.Extensions;
using G4.Models;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using System;
using System.Linq;
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
            // Log the new connection for monitoring/debugging purposes
            _logger.LogInformation("Bot connected: {ConnectionId}", Context.ConnectionId);

            // Continue with base hub connection logic
            await base.OnConnectedAsync();
        }

        /// <inheritdoc />
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            // Get all bot IDs associated with the current connection
            var ids = _domain.Bots.ConnectedBots.Keys
                .Where(i => i.Contains(Context.ConnectionId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Attempt to remove the bot entry from the connected bots dictionary
            foreach (var id in ids)
            {
                // Remove the bot from the connected bots dictionary
                _domain.Bots.ConnectedBots.TryRemove(id, out _);
            }

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
        public async Task RegisterBot(ConnectedBotModel registrationRequest)
        {
            // Initialize the bot ID based on the provided ID or fallback to the connection ID
            var id = string.IsNullOrEmpty(registrationRequest.Id)
                ? Context.ConnectionId
                : $"{registrationRequest.Id}-{Context.ConnectionId}";

            // Use the provided ID or fallback to the connection ID
            var connectedBot = new ConnectedBotModel
            {
                ConnectionId = Context.ConnectionId,
                Id = id
            };

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

            // Add the bot to the connected bots dictionary
            _domain.Bots.ConnectedBots[connectedBot.Id] = connectedBot;

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
        public async Task UpdateBot(ConnectedBotModel updateRequest)
        {
            // Initialize the bot ID based on the provided ID or fallback to the connection ID
            var id = string.IsNullOrEmpty(updateRequest.Id)
                ? Context.ConnectionId
                : $"{updateRequest.Id}-{Context.ConnectionId}";

            // Attempt to retrieve the bot associated with the current connection
            var isConnected = _domain.Bots.ConnectedBots.TryGetValue(id, out var connectedBot);

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
