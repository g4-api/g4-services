using G4.Abstraction.Logging;
using G4.Api;
using G4.Cache;
using G4.Models;
using G4.Services.Domain.V4.Hubs;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Text.Json;

namespace G4.Services.Domain.V4
{
    /// <summary>
    /// Represents the G4 domain, providing access to core services and clients.
    /// </summary>
    /// <param name="cache">The cache manager instance.</param>
    /// <param name="g4Client">The G4 client instance.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="jsonOptions">The JSON serializer options.</param>
    public class G4Domain(
        CacheManager cache,
        G4Client g4Client,
        ILogger logger,
        JsonSerializerOptions jsonOptions,
        IHubContext<G4AutomationNotificationsHub> notificationsHubContext) : IDomain
    {
        #region *** Properties ***
        /// <inheritdoc />
        public CacheManager Cache { get; set; } = cache;

        /// <inheritdoc />
        public G4Client G4Client { get; set; } = g4Client;

        /// <inheritdoc />
        public JsonSerializerOptions JsonOptions { get; set; } = jsonOptions;

        /// <inheritdoc />
        public ILogger Logger { get; set; } = logger;

        public IHubContext<G4AutomationNotificationsHub> NotificationsHubContext { get; set; } = notificationsHubContext;
        #endregion

        #region *** Methods    ***
        /// <summary>
        /// Configures and registers the necessary dependencies for the G4 domain.
        /// </summary>
        /// <param name="builder">The web application builder used to register services.</param>
        public static void SetDependencies(WebApplicationBuilder builder)
        {
            // Get the singleton instance of the cache manager
            var cache = CacheManager.Instance;

            // Register the cache manager as a singleton service
            builder.Services.AddSingleton(implementationInstance: cache);

            // Register a new G4 client using the cache manager
            builder.Services.AddSingleton((serviceProvider) => NewClient(serviceProvider, cache));

            // Register the G4 logger instance
            builder.Services.AddSingleton(implementationInstance: G4Logger.Instance);

            // Register the JSON serializer options from the configuration
            builder
                .Services
                .AddSingleton(implementationFactory: provider => provider
                    .GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions);

            // Register the LiteDB instance from the cache manager
            builder.Services.AddSingleton(CacheManager.LiteDatabase);

            // Register the G4Domain as a transient service implementing IDomain
            builder.Services.AddTransient<IDomain, G4Domain>();
        }

        // Creates a new instance of G4Client and sets up event handlers for automation notifications.
        private static G4Client NewClient(IServiceProvider serviceProvider, CacheManager cache)
        {
            // Retrieves the connection ID from the automation model's environment variables.
            static string GetConnection(G4AutomationModel automation)
            {
                // Access the "SignalR" environment variable and retrieve the "ConnectionId" parameter.
                // If the parameter is not found, return null.
                return automation
                    .Settings
                    .EnvironmentsSettings
                    .EnvironmentVariables["SignalR"]
                    .Parameters["ConnectionId"]
                    ?.ToString();
            }

            // Sends a message to a specific client through SignalR.
            static void SendMessage(
                IHubContext<G4AutomationNotificationsHub> context,
                string connectionId,
                string method,
                object message)
            {
                // Use the SignalR context to send the specified message to the client identified by the connection ID.
                context.Clients.Client(connectionId).SendAsync(method, message);
            }

            // Resolve the SignalR notifications hub from the service provider.
            var context = serviceProvider.GetRequiredService<IHubContext<G4AutomationNotificationsHub>>();

            // Initialize the G4Client with the provided cache manager.
            var client = new G4Client(cache);

            // Set up the event handler for when an automation is completed.
            client.Automation.AutomationInvoked += (sender, e) =>
            {
                // Retrieve the connection ID for SignalR from environment variables.
                var connectionId = GetConnection(e.Automation);

                // Send a notification about the completion of the automation to the specified SignalR client.
                SendMessage(context, connectionId, method: "ReceiveDefinitionCompleteEvent", message: null);
            };

            // Set up the event handler for when an automation is invoked.
            client.Automation.AutomationInvoking += (sender, e) =>
            {
                // Retrieve the connection ID for SignalR from environment variables.
                var connectionId = GetConnection(e.Automation);

                // Send a notification about the automation invocation to the specified SignalR client.
                SendMessage(context, connectionId, method: "ReceiveAutomationStartEvent", message: new
                {
                    Type = "Automation",
                    e.Automation.Reference.Id
                });
            };

            // Set up the event handler for when a stage is invoked.
            client.Automation.StageInvoking += (sender, e) =>
            {
                // Retrieve the connection ID for SignalR from environment variables.
                var connectionId = GetConnection(e.Automation);

                // Send a notification about the stage invocation to the specified SignalR client.
                SendMessage(context, connectionId, method: "ReceiveAutomationStartEvent", message: new
                {
                    Type = "Stage",
                    e.Stage.Reference.Id
                });
            };

            // Set up the event handler for when a job is invoked.
            client.Automation.JobInvoking += (sender, e) =>
            {
                // Retrieve the connection ID for SignalR from environment variables.
                var connectionId = GetConnection(e.Automation);

                // Send a notification about the job invocation to the specified SignalR client.
                SendMessage(context, connectionId, method: "ReceiveAutomationStartEvent", message: new
                {
                    Type = "Job",
                    e.Job.Reference.Id
                });
            };

            // Set up the event handler for when a rule is invoked.
            client.Automation.RuleInvoking += (sender, e) =>
            {
                // Retrieve the connection ID for SignalR from environment variables.
                var connectionId = GetConnection(e.Automation);

                // Send a notification about the rule invocation to the specified SignalR client.
                SendMessage(context, connectionId, method: "ReceiveAutomationStartEvent", message: new {
                    Type = "Rule",
                    e.Rule.Reference.Id
                });
            };

            // Return the fully configured client instance.
            return client;
        }
        #endregion
    }
}
