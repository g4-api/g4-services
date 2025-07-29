using G4.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Hubs
{
    public class G4AutomationNotificationsHub(IDomain domain) : Hub
    {
        // The domain service for the G4™ engine.
        private readonly IDomain _domain = domain;

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
        public Task StartAutomation([FromBody] G4AutomationModel automation)
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

            // Invoke the automation session using the automation model, returning a response object.
            var response = _domain.G4.Automation.Invoke(automation);

            // Send the response back to the calling client via the "StartAutomation" method.
            return Clients.Caller.SendAsync("StartAutomation", response);
        }
    }
}
