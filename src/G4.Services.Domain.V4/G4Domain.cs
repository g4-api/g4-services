using G4.Abstraction.Logging;
using G4.Api;
using G4.Cache;
using G4.Extensions;
using G4.Models;
using G4.Models.Events;
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
        #region *** Properties   ***
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

        #region *** Methods      ***
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
                // Check if the EnvironmentVariables collection is not null
                var isEnvironmentVariables = automation?.Settings?.EnvironmentsSettings?.EnvironmentVariables != null;

                // Attempt to retrieve the "SignalR" environment from EnvironmentVariables
                var isSignalR = isEnvironmentVariables && automation.Settings.EnvironmentsSettings.EnvironmentVariables.ContainsKey("SignalR");

                // Check if the retrieved environment has a valid Parameters dictionary
                var isParameters = isSignalR && automation.Settings.EnvironmentsSettings.EnvironmentVariables["SignalR"]?.Parameters != null;

                // Attempt to retrieve "ConnectionId" from the environment's Parameters
                var isConnectionId = isParameters && automation.Settings.EnvironmentsSettings.EnvironmentVariables["SignalR"].Parameters.ContainsKey("ConnectionId");

                // If all checks pass, return the connectionId; otherwise, return an empty string
                return isConnectionId
                    ? $"{automation.Settings.EnvironmentsSettings.EnvironmentVariables["SignalR"].Parameters["ConnectionId"]}"
                    : string.Empty;
            }

            // Sends a message to a specific client through SignalR.
            static void SendMessage(
                IHubContext<G4AutomationNotificationsHub> context,
                string connectionId,
                string method,
                object message)
            {
                // If the connection ID is null or empty, return without sending the message.
                if (string.IsNullOrEmpty(connectionId))
                {
                    return;
                }

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
                SendMessage(context, connectionId, method: "ReceiveAutomationInvokedEvent", message: new EventDataModel
                {
                    Id = e.Automation.Reference.Id,
                    ObjectType = nameof(G4AutomationModel),
                    Type = "Automation",
                    Value = e.Automation
                });
            };

            // Set up the event handler for when an automation is invoked.
            client.Automation.AutomationInvoking += (sender, e) =>
            {
                // Retrieve the connection ID for SignalR from environment variables.
                var connectionId = GetConnection(e.Automation);

                // Send a notification about the automation invocation to the specified SignalR client.
                SendMessage(context, connectionId, method: "ReceiveAutomationStartEvent", message: new EventDataModel
                {
                    Id = e.Automation.Reference.Id,
                    ObjectType = nameof(G4AutomationModel),
                    Type = "Automation",
                    Value = e.Automation
                });
            };

            // Set up the event handler for when a stage is invoked.
            client.Automation.StageInvoking += (sender, e) =>
            {
                // Retrieve the connection ID for SignalR from environment variables.
                var connectionId = GetConnection(e.Automation);

                // Send a notification about the stage invocation to the specified SignalR client.
                SendMessage(context, connectionId, method: "ReceiveAutomationStartEvent", message: new EventDataModel
                {
                    Id = e.Stage.Reference.Id,
                    ObjectType = nameof(G4StageModel),
                    Type = "Stage",
                    Value = e.Stage
                });
            };

            // Set up the event handler for when a job is invoked.
            client.Automation.JobInvoking += (sender, e) =>
            {
                // Retrieve the connection ID for SignalR from environment variables.
                var connectionId = GetConnection(e.Automation);

                // Send a notification about the job invocation to the specified SignalR client.
                SendMessage(context, connectionId, method: "ReceiveAutomationStartEvent", message: new EventDataModel
                {
                    Id = e.Job.Reference.Id,
                    ObjectType = nameof(G4JobModel),
                    Type = "Job",
                    Value = e.Job
                });
            };

            // Set up the event handler for when a rule is invoked.
            client.Automation.RuleInvoked += (sender, e) =>
            {
                // Retrieve the connection ID for SignalR from environment variables.
                var connectionId = GetConnection(e.Automation);

                // Send a notification about the rule invocation to the specified SignalR client.
                SendMessage(context, connectionId, method: "ReceiveAutomationEndEvent", message: new EventDataModel
                {
                    Id = e.Rule.Reference.Id,
                    ObjectType = e.Rule.GetType().Name,
                    Type = e.Rule.GetManifest().PluginType,
                    Value = e.Rule
                });
            };

            // Set up the event handler for when a rule is invoked.
            client.Automation.RuleInvoking += (sender, e) =>
            {
                // Retrieve the connection ID for SignalR from environment variables.
                var connectionId = GetConnection(e.Automation);

                // Send a notification about the rule invocation to the specified SignalR client.
                SendMessage(context, connectionId, method: "ReceiveAutomationStartEvent", message: new EventDataModel
                {
                    Id = e.Rule.Reference.Id,
                    ObjectType = e.Rule.GetType().Name,
                    Type = e.Rule.GetManifest().PluginType,
                    Value = e.Rule
                });
            };

            // Set up the event handler for when an automation request is initialized.
            client.Automation.AutomationRequestInitialized += (_, e) =>
            {
                // Retrieve the connection ID for SignalR from environment variables.
                var connectionId = GetConnection(e.Status.Automation);

                // Send a notification about the automation request initialization to the specified SignalR client.
                SendMessage(context, connectionId, method: "ReceiveAutomationRequestInitializedEvent", message: new EventDataModel
                {
                    Id = e.Status.Automation.Reference.Id,
                    ObjectType = nameof(G4AutomationModel),
                    Type = "Automation",
                    Value = e.Status.Automation
                });
            };

            // Set up the event handler for when a log is being created.
            client.Automation.LogCreating += (_, e) =>
            {
                // Retrieve the connection ID for SignalR from environment variables.
                var connectionId = GetConnection(e.Automation);

                // Send a notification about the log creation to the specified SignalR client.
                SendMessage(context, connectionId, method: "ReceiveLogCreatingEvent", message: new EventDataModel
                {
                    Id = e.Invoker,
                    ObjectType = nameof(LogEventArgs),
                    Type = "Log",
                    Value = e.LogMessage
                });
            };

            // Set up the event handler for when a log is created.
            client.Automation.LogCreated += (_, e) =>
            {
                // Retrieve the connection ID for SignalR from environment variables.
                var connectionId = GetConnection(e.Automation);

                // Send a notification about the log creation to the specified SignalR client.
                SendMessage(context, connectionId, method: "ReceiveLogCreatedEvent", message: new EventDataModel
                {
                    Id = e.Invoker,
                    ObjectType = nameof(LogEventArgs),
                    Type = "Log",
                    Value = e.LogMessage
                });
            };

            // Return the fully configured client instance.
            return client;
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents a data model for an event used in client integrations.
        /// </summary>
        private sealed class EventDataModel
        {
            /// <summary>
            /// Gets or sets the unique identifier for the entity in use.
            /// This identifier is used in client integrations to tie the event
            /// back to a specific entity instance.
            /// </summary>
            public string Id { get; set; }

            // The Id property is used for client integrations. 
            // It typically corresponds to the primary key of the underlying entity 
            // (e.g., a GUID or numeric ID).

            /// <summary>
            /// Gets or sets the name of the underlying class of the entity being used.
            /// </summary>
            public string ObjectType { get; set; }

            // The ObjectType property stores the type name (class name) of the entity 
            // involved in this event. This allows clients or downstream services to 
            // understand which entity type is referenced.

            /// <summary>
            /// Gets or sets the expressive entity type, as defined in the plugin manifest
            /// or hardcoded in the application.
            /// </summary>
            public string Type { get; set; }

            // The Type property serves as a high-level classification or "label" 
            // for the entity/event, often derived from a plugin manifest, 
            // configuration, or code constants.

            /// <summary>
            /// Gets or sets the complete state of the entity at the time of the event.
            /// </summary>
            public object Value { get; set; }

            // The Value property captures the full snapshot of the entity's state 
            // when this event occurred. This is especially useful for auditing, 
            // logging, or downstream processes needing to know the exact entity data 
            // at the event's moment in time.
        }
        #endregion
    }
}
