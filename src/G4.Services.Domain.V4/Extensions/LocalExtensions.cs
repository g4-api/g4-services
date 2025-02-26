using G4.Models;

using Microsoft.AspNetCore.SignalR;

namespace G4.Extensions
{
    internal static class LocalExtensions
    {
        /// <summary>
        /// Retrieves the connection identifier from the G4AutomationModel's SignalR environment settings.
        /// </summary>
        /// <param name="automation">The G4AutomationModel instance containing the environment settings.</param>
        /// <returns>The connection identifier if it exists in the SignalR environment settings; otherwise, an empty string.</returns>
        public static string GetConnection(this G4AutomationModel automation)
        {
            // Retrieve the collection of environment variables from the automation settings.
            var environmentVariables = automation?.Settings?.EnvironmentsSettings?.EnvironmentVariables;

            // Check if the EnvironmentVariables collection exists.
            bool environmentVariablesExist = environmentVariables != null;

            // Verify that the "SignalR" key is present in the EnvironmentVariables collection.
            bool containsSignalREntry = environmentVariablesExist && environmentVariables.ContainsKey("SignalR");

            // Confirm that the "SignalR" entry includes a non-null Parameters dictionary.
            bool hasParameters = containsSignalREntry && environmentVariables["SignalR"]?.Parameters != null;

            // Determine if the Parameters dictionary contains the "ConnectionId" key.
            bool containsConnectionId = hasParameters && environmentVariables["SignalR"].Parameters.ContainsKey("ConnectionId");

            // Return the connection identifier if available; otherwise, return an empty string.
            return containsConnectionId
                ? $"{environmentVariables["SignalR"].Parameters["ConnectionId"]}"
                : string.Empty;
        }

        /// <summary>
        /// Sends a message to a specific client using the SignalR hub context.
        /// </summary>
        /// <typeparam name="T">The type of the Hub.</typeparam>
        /// <param name="context">The hub context used to send the message.</param>
        /// <param name="connectionId">The unique identifier for the target client connection.</param>
        /// <param name="method">The name of the method to be invoked on the client.</param>
        /// <param name="message">The message payload to be sent.</param>
        public static void SendMessage<T>(this IHubContext<T> context, string connectionId, string method, object message)
            where T : Hub
        {
            // If the connection ID is null or empty, do not proceed with sending the message.
            if (string.IsNullOrEmpty(connectionId))
            {
                return;
            }

            // Send the specified message to the client with the given connection ID using the provided method.
            context.Clients.Client(connectionId).SendAsync(method, message);
        }
    }
}
