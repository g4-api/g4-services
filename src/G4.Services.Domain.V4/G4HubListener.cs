using G4.Api;
using G4.Models;
using G4.Settings;

using Microsoft.AspNetCore.SignalR.Client;

using System;
using System.Linq;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4
{
    /// <summary>
    /// Represents a listener that connects to a G4 hub, maintains a heartbeat,
    /// and periodically checks for pending automations.
    /// </summary>
    public class G4HubListener
    {
        // The underlying HubConnection used to communicate with the G4 hub.
        private readonly HubConnection _hubConnection;

        // A flag indicating whether the listener is currently locked (e.g., during a pending automation).
        private bool _isLocked;

        /// <summary>
        /// Initializes a new instance of the <see cref="G4HubListener"/> class,
        /// automatically resolving the hub URI and starting the connection.
        /// </summary>
        public G4HubListener()
            : this(hubUri: ResolveHubUri())
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="G4HubListener"/> class,
        /// connecting to the specified <see cref="Uri"/>.
        /// </summary>
        /// <param name="hubUri">The URI of the G4 hub to connect to.</param>
        public G4HubListener(Uri hubUri)
        {
            // Start the hub connection and store the result.
            _hubConnection = StartHubConnectionAsync(hubUri).GetAwaiter().GetResult();

            // Subscribe to hub events, sending this listener instance for potential locking checks.
            InitializeHubConnection(this, _hubConnection);

            // Start sending heartbeat signals to the G4 hub.
            StartHeartbeatListener(_hubConnection);
        }

        // TODO: Control interval with a configuration setting.
        /// <summary>
        /// Begins listening for pending automation tasks in a continuous loop,
        /// polling the G4 hub every second when not locked by a current automation task.
        /// </summary>
        public void StartG4HubListener()
        {
            // Use Task.Factory.StartNew to run a background loop.
            Task.Factory.StartNew(async () =>
            {
                // Continuously poll the hub for pending automation tasks.
                while (true)
                {
                    // Only request pending automation if the listener is not locked.
                    if (!_isLocked)
                    {
                        GetPendingAutomation(_hubConnection);
                    }

                    // Wait for 1 second before the next polling attempt.
                    await Task.Delay(1000);
                }
            });
        }

        // Sends a request to retrieve any pending automation from the specified HubConnection.
        private static void GetPendingAutomation(HubConnection hubConnection)
        {
            try
            {
                // Send an asynchronous request to the hub to get pending automation.
                hubConnection.SendAsync("GetPendingAutomation");
            }
            catch (Exception e)
            {
                // Log the exception details to the console if an error occurs.
                Console.WriteLine($"Error getting pending automation: {e}");
            }
        }

        // Initializes the specified <see cref="HubConnection"/> by subscribing to hub events.
        private static void InitializeHubConnection(G4HubListener listener, HubConnection hubConnection)
        {
            // Subscribe to the "ReceiveHeartbeat" event, invoking ReceiveHeartbeat on notification.
            hubConnection.On<string>("ReceiveHeartbeat", ReceiveHeartbeat);

            // Subscribe to the "ReceivePendingAutomation" event, invoking ReceivePendingAutomation on notification.
            hubConnection.On<G4QueueModel>("ReceivePendingAutomation", (queueModel)
                => ReceivePendingAutomation(listener, hubConnection, queueModel));
        }

        // Logs a heartbeat message to the console with the current date and time.
        private static void ReceiveHeartbeat(string message)
        {
            // Log the heartbeat message with a timestamp.
            Console.WriteLine($"{DateTime.Now:yyyy/MM/dd hh:mm:ss}: {message}");
        }

        // Receives a G4QueueModel containing automation details and invokes the automation using a G4Client.
        // Locks the G4HubListener while automation is in progress and unlocks it on completion.
        private static void ReceivePendingAutomation(G4HubListener listener, HubConnection hubConnection, G4QueueModel queueModel)
        {
            try
            {
                // Lock the listener to prevent other automations from starting concurrently.
                listener._isLocked = true;

                // If no automation data is provided, exit the method.
                if (queueModel == null)
                {
                    return;
                }

                // Retrieve the unique automation ID from the queue model for logging purposes.
                var id = queueModel.Automation.Reference.Id;

                // Log the start time and the automation ID.
                Console.WriteLine($"{DateTime.Now:yyyy/MM/dd hh:mm:ss}: Running pending automation {id}...");

                // Create a new G4Client instance to invoke the pending automation.
                var client = new G4Client();

                // Invoke the automation request stored in the queue model, then capture the response.
                var response = client.Automation.Invoke(queueModel);

                // Log completion of the automation.
                Console.WriteLine($"{DateTime.Now:yyyy/MM/dd hh:mm:ss}: Automation {id} completed.");

                // Notify the hub connection that the automation has completed,
                // sending the first response key-value pair as the completion data.
                hubConnection.SendAsync("CompleteAutomation", response.First().Key, response.First().Value);
            }
            finally
            {
                // Unlock the listener to allow subsequent automations to run.
                listener._isLocked = false;
            }
        }

        // Resolves the hub address based on command-line arguments, environment variables, or application settings.
        // If none of these sources provide an address, a default value is used.
        private static Uri ResolveHubUri()
        {
            // Define the default hub address.
            const string defaultHubAddress = "http://localhost:9944";

            // Create a Uri instance from the default address.
            var hubUri = new Uri(defaultHubAddress);

            // Check if an environment variable for the hub address is set.
            var envHubAddress = Environment.GetEnvironmentVariable("HUB_URI");

            // Retrieve the hub address from the application settings.
            var settingsHubAddress = AppSettings.Configuration.GetSection("G4:WorkerConfiguration:HubUri").Value;

            try
            {
                // Return the environment variable value if it exists.
                if (envHubAddress != null)
                {
                    return new Uri(envHubAddress);
                }
                // Otherwise, return the settings value if it exists.
                else if (settingsHubAddress != null)
                {
                    return new Uri(settingsHubAddress);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error resolving hub address: {e.Message}");
            }

            // If neither are set, return the default hub address.
            return hubUri;
        }

        // Starts a background listener task that periodically sends a "SendHeartbeat" message
        private static void StartHeartbeatListener(HubConnection hubConnection)
        {
            // Use Task.Factory.StartNew to run a background loop.
            // This ensures the main thread won't be blocked.
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    // Send the "SendHeartbeat" message to the hub.
                    await hubConnection.SendAsync("SendHeartbeat");

                    // Wait for 10 seconds before sending the next heartbeat.
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            });
        }

        // Creates a new HubConnection, attempts to connect to the specified hub URI, 
        // and sets up automatic reconnection on connection loss.
        private static async Task<HubConnection> StartHubConnectionAsync(Uri hubUri)
        {
            // Continuously attempts to start the hub connection until a successful connection is made.
            static async Task ConnectAsync(HubConnection connection, Uri hubUri)
            {
                // Continuously attempt to start the connection until successful.
                while (true)
                {
                    try
                    {
                        // Attempt to start the hub connection.
                        await connection.StartAsync();
                        Console.WriteLine("Client is connected and listening for automation jobs.");
                        Console.WriteLine();

                        // Exit the loop when the connection is successfully started.
                        break;
                    }
                    catch (Exception e)
                    {
                        // Log the failure message and retry after a delay.
                        Console.WriteLine($"Error connecting to the hub {hubUri.AbsoluteUri}: {e.Message}");
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }
                }
            }

            // Define the relative route to the hub endpoint.
            const string route = "hub/v4/g4/orchestrator";

            // Build the HubConnection without using the built-in automatic reconnect feature.
            var connection = new HubConnectionBuilder()
                .WithUrl($"{hubUri.AbsoluteUri.TrimEnd('/')}/{route}")
                .Build();

            // Register a handler for when the connection is closed.
            // This will attempt to reconnect when the connection is lost.
            connection.Closed += async (error) =>
            {
                Console.WriteLine($"Connection closed due to error: {error?.Message}. Attempting to reconnect...");
                await ConnectAsync(connection, hubUri);
            };

            // Establish the initial connection.
            await ConnectAsync(connection, hubUri);

            // Return the established and connected HubConnection.
            return connection;
        }
    }
}
