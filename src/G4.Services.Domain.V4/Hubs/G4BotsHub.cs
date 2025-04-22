using G4.Extensions;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
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
                Description = string.Empty,       // Will be provided later via RegisterBot
                Machine = string.Empty,           // Will be provided later via RegisterBot
                Type = string.Empty,              // Will be provided later via RegisterBot
                Status = "Connected",             // Set initial status to "Connected"
                IsContainer = false,              // Assume non-container by default (can be updated later)
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
        public async Task RegisterBot(Dictionary<string, object> data)
        {
            // Attempt to retrieve the bot associated with the current connection
            var isConnected = _domain.ConnectedBots.TryGetValue(Context.ConnectionId, out var bot);

            // Connection not found in the connected bot registry — exit early
            if (!isConnected)
            {
                return;
            }

            // Update the bot's name if provided
            if (data.TryGetValue("Name", out var name))
            {
                bot.Name = name?.ToString() ?? bot.Name;
            }

            // Update the bot's description if provided
            if (data.TryGetValue("Description", out var desc))
            {
                bot.Description = desc?.ToString() ?? bot.Description;
            }

            // Update the bot's type if provided
            if (data.TryGetValue("Type", out var type))
            {
                bot.Type = type?.ToString() ?? bot.Type;
            }

            // Update the bot's machine name if provided
            if (data.TryGetValue("Machine", out var machine))
            {
                bot.Machine = machine?.ToString() ?? bot.Machine;
            }

            // Update the bot's IsContainer property if provided
            if (data.TryGetValue("IsContainer", out var isContainerObj) &&
                bool.TryParse(isContainerObj?.ToString(), out var isContainer))
            {
                bot.IsContainer = isContainer;
            }

            // Update the modification timestamp to reflect changes
            bot.LastModifiedOn = DateTime.UtcNow;

            // Log the registration event with key bot details
            _logger.LogInformation(
                message: "Bot registered: {ConnectionId} Name:{Name} Type:{Type}",
                Context.ConnectionId, bot.Name, bot.Type
            );

            // Send an acknowledgment back to the client
            await Clients.Caller.SendAsync("ReceiveRegisterBot", new
            {
                StatusCode = 200,
                Message = $"Bot '{bot.Name}' registered successfully."
            });
        }

        // Updates the status of the connected bot.
        // This method is typically used to reflect real-time state changes (e.g., Online, Idle, Busy).
        [HubMethodName(nameof(UpdateBot))]
        public async Task UpdateBot(string status)
        {
            // Attempt to retrieve the bot associated with the current connection
            var isConnected = _domain.ConnectedBots.TryGetValue(Context.ConnectionId, out var bot);

            if (!isConnected)
            {
                // Connection not found in the connected bot registry — exit early
                return;
            }

            // Update the bot's operational status
            bot.Status = status;

            // Refresh the last modified timestamp
            bot.LastModifiedOn = DateTime.UtcNow;

            // Log the update for auditing or monitoring purposes
            _logger.LogInformation(
                message: "Bot updated: {ConnectionId}, Status:{Status}",
                Context.ConnectionId,
                bot.Status
            );

            // Notify the caller with the updated bot model
            await Clients.Caller.SendAsync("ReceiveBotUpdated", bot);
        }
    }
}
