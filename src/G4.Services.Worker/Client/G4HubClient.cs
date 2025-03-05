using Microsoft.AspNetCore.SignalR.Client;
using System;

namespace G4.Services.Worker.Client
{
    public class G4HubClient(Uri hubUri)
    {
        private readonly HubConnection _hubConnection = NewHubClient(hubUri);

        public void GetAutomationRequest()
        {
            _hubConnection.On<string>("ReceiveAutomationRequest", (request) =>
            {
                Console.WriteLine($"Received automation request: {request}");
            });
        }

        public void SendAutomationStatus(string status)
        {
            _hubConnection.SendAsync("SendAutomationStatus", status);
        }

        private static HubConnection NewHubClient(Uri hubUri)
        {
            // Create the connection
            var connection = new HubConnectionBuilder()
                .WithUrl($"{hubUri.AbsoluteUri.TrimEnd('/')}/hub/v4/g4/orchestrator")
                .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10)])
                .Build();

            var route = "/hub/v4/g4/orchestrator";

            try
            {
                // Start the connection
                connection.StartAsync().GetAwaiter().GetResult();
                Console.WriteLine("Connected to the hub.");

            }
            catch (Exception e)
            {
                Console.WriteLine($"Connection error: {e.Message}");
            }

            return connection;
        }
    }
}
