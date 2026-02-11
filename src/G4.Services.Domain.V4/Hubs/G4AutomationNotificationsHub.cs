using G4.Extensions;
using G4.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Hubs
{
    public class G4AutomationNotificationsHub(IDomain domain) : Hub
    {
        // The domain service for the G4™ engine.
        private readonly IDomain _domain = domain;

        // Handles the "InitializeAutomation" method invoked by clients. It initializes an automation session
        // using the provided automation model and sends the response back to the caller.
        [HubMethodName("InitializeAutomation")]
        public Task InitializeAutomation([FromBody] G4AutomationModel automation)
        {
            // Invoke the automation session using the automation model, returning a response object.
            var response = automation.Initialize();

            // Send the response back to the calling client via the "StartAutomation" method.
            return Clients.Caller.SendAsync("InitializeAutomation", response);
        }

        // Handles the "SendHeartbeat" method invoked by clients. Logs the connection ID
        // and responds with a heartbeat acknowledgment.
        [HubMethodName("SendHeartbeat")]
        public Task SendHeartbeat()
        {
            // Send a heartbeat acknowledgment back to the calling client.
            return Clients.Caller.SendAsync("SendHeartbeat", "Heartbeat received, connection is active.");
        }

        // Asynchronously handles an automation request by preparing environment variables,
        // invoking the automation session using the provided model, and sending the result
        [HubMethodName("StartAutomation")]
        [SuppressMessage("Style", "IDE0039:Use local function", Justification = "The function needs to be passed as a delegate and removed from client singleton.")]
        public async Task StartAutomation([FromBody] G4AutomationModel automation)
        {
            // Obtain or create the environment settings from the automation model.
            // If automation.Settings.EnvironmentsSettings is null, create a new EnvironmentsSettingsModel.
            var settings = automation.Settings.EnvironmentsSettings ?? new EnvironmentsSettingsModel();

            // Create a dictionary to hold environment-specific variables.
            settings.EnvironmentVariables = new Dictionary<string, ApplicationParametersModel>(StringComparer.OrdinalIgnoreCase)
            {
                // Add an environment variable for SignalR, storing the caller's connection ID.
                ["SignalR"] = new ApplicationParametersModel
                {
                    Name = "SignalR",
                    Parameters = new Dictionary<string, object>
                    {
                        ["ConnectionId"] = Context.ConnectionId
                    }
                }
            };

            // Ensures continuations do not run inline (prevents potential deadlocks)
            var creationOptions = TaskCreationOptions.RunContinuationsAsynchronously;

            // TaskCompletionSource used to detect AutomationStopped event
            var taskCompletionSource = new TaskCompletionSource<object>(creationOptions);

            // Event handler triggered when automation stops externally
            EventHandler<int> handler = (sender, args) =>
            {
                taskCompletionSource.TrySetResult(null);
            };

            // Subscribe to stopped event
            _domain.G4.Automation.AutomationStopped += handler;

            try
            {
                // Run automation execution on background thread
                var automationTask = Task.Run(() => _domain.G4.Automation.Invoke(automation));

                // Wait for either:
                //   1. Automation completion
                //   2. Automation stopped event
                var completedTask = await Task.WhenAny(automationTask, taskCompletionSource.Task);

                // If stopped event completed first → canceled scenario
                if (completedTask == taskCompletionSource.Task)
                {
                    // Send cancellation response back to client and exit
                    await Clients.Caller.SendAsync("StartAutomation", new GenericErrorModel()
                    {
                        Errors = new Dictionary<string, string[]>
                        {
                            { "Automation", ["Automation stopped"] }
                        }
                    });

                    // Exit without awaiting the automation task when stopped,
                    // which may still be running in the background
                    return;
                }

                // Otherwise automation completed normally
                var response = await automationTask;

                // Send the successful response back to the calling client via the "StartAutomation" method.
                await Clients.Caller.SendAsync("StartAutomation", response);
            }
            catch (Exception ex)
            {
                // Send the error response back to the calling client via the "StartAutomation" method.
                await Clients.Caller.SendAsync("StartAutomation", new GenericErrorModel()
                {
                    Errors = new Dictionary<string, string[]>
                    {
                        { "Automation", new[] { ex.Message } }
                    },
                    Request = automation
                });
            }
            finally
            {
                // Always unsubscribe to prevent memory leaks
                _domain.G4.Automation.AutomationStopped -= handler;
            }
        }
    }
}
