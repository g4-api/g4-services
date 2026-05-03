using G4.Abstraction.Cli;
using G4.Abstraction.Logging;
using G4.Api;
using G4.Cache;
using G4.Extensions;
using G4.Models;
using G4.Models.Events;
using G4.Services.Domain.V4.Clients;
using G4.Services.Domain.V4.Extensions;
using G4.Services.Domain.V4.Hubs;
using G4.Services.Domain.V4.Repositories;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.IO;
using System.Text.Json;

namespace G4.Services.Domain.V4
{
    /// <summary>
    /// Represents the core domain services and application state for the G4 platform.
    /// Exposes repositories, hub contexts, caching, serialization settings, and logging.
    /// </summary>
    public interface IDomain
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the adapter for ASP.NET Core hosting environment and related services.
        /// </summary>
        IAspAdapter Asp { get; set; }

        /// <summary>
        /// Gets or sets the adapter used for G4 operations.
        /// </summary>
        IG4Adapter G4 { get; set; }

        /// <summary>
        /// Gets or sets the adapter used to manage SignalR hubs within the application.
        /// </summary>
        IHubsAdapter Hubs { get; set; }

        /// <summary>
        /// Gets or sets the adapter responsible for managing application resources,
        /// such as SVGs and other static content.
        /// </summary>
        IResourcesAdapter Resources { get; set; }



        ///// <summary>
        ///// Gets or sets the repository for managing bot entities.
        ///// </summary>
        //IBotsRepository Bots { get; set; }

        ///// <summary>
        ///// Gets or sets the SignalR hub context for communicating with bot-specific clients.
        ///// </summary>
        //IHubContext<G4BotsHub> BotsHubContext { get; set; }

        ///// <summary>
        ///// Gets or sets the cache manager responsible for storing and retrieving
        ///// domain-level cache entries to improve performance.
        ///// </summary>
        //CacheManager Cache { get; set; }

        ///// <summary>
        ///// Gets or sets the factory used to parse values to and from the G4 CLI format.
        ///// </summary>
        ///// <remarks>
        ///// This property is initialized with a default <see cref="CliFactory"/> instance
        ///// and is used to handle conversions between runtime values and their
        ///// command-line representation in the G4 CLI format.
        ///// </remarks>
        //CliFactory CliFactory { get; set; }

        ///// <summary>
        ///// Gets or sets the hosting environment information (e.g., Development, Production).
        ///// </summary>
        //IWebHostEnvironment Environment { get; set; }

        ///// <summary>
        ///// Gets or sets the G4 API client used to perform operations against external services,
        ///// such as job dispatching and status retrieval.
        ///// </summary>
        //G4Client G4 { get; set; }

        ///// <summary>
        ///// Gets or sets the SignalR hub context for broadcasting general G4 notifications.
        ///// </summary>
        //IHubContext<G4Hub> G4HubContext { get; set; }

        ///// <summary>
        ///// Gets or sets the JSON serializer options controlling converters,
        ///// naming policies, and formatting rules for domain serialization.
        ///// </summary>
        //JsonSerializerOptions JsonOptions { get; set; }

        ///// <summary>
        ///// Gets or sets the logger used to record domain events,
        ///// diagnostics, and error information.
        ///// </summary>
        //ILogger Logger { get; set; }

        ///// <summary>
        ///// Gets or sets the repository for MCP integration and data operations.
        ///// </summary>
        //IMcpRepository Mcp { get; set; }

        ///// <summary>
        ///// Gets or sets the SignalR hub context for sending automation notifications
        ///// and updates to subscribed clients.
        ///// </summary>
        //IHubContext<G4AutomationNotificationsHub> NotificationsHubContext { get; set; }

        ///// <summary>
        ///// Gets or sets the OpenAI client used to interact with OpenAI services.
        ///// </summary>
        //IOpenAiClient OpenAi { get; set; }

        ///// <summary>
        ///// Gets or sets the cache model used for storing and retrieving SVG resources.
        ///// </summary>
        //SvgCacheModel SvgCache { get; set; }

        ///// <summary>
        ///// Gets or sets the repository for managing tool entities and operations.
        ///// </summary>
        //IToolsRepository Tools { get; set; }
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
            var connectedBots = IBotsRepository.InitializeConnectedBots(CacheManager.LiteDatabase, G4Logger.Instance);
            var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");

            // Register the cache manager as a singleton service
            builder.Services.AddSingleton(implementationInstance: cache);

            // Register a new G4 client using the cache manager
            builder.Services.AddSingleton((serviceProvider) => NewG4Client(serviceProvider, cache));

            // Register the G4 logger instance
            builder.Services.AddSingleton(implementationInstance: G4Logger.Instance);

            // Register the JSON serializer options from the configuration
            builder
                .Services
                .AddSingleton(implementationFactory: provider => provider
                    .GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions);

            // Register the LiteDB instance from the cache manager
            builder.Services.AddSingleton(CacheManager.LiteDatabase);

            // TODO: Use QueueManagerFactory to automatically resolve the queue manager implementation.
            // Register the queue manager as a singleton service implementing IQueueManager interface.
            builder.Services.AddSingleton<IQueueManager, BasicQueueManager>();

            // Register bot cache model as a singleton service
            builder.Services.AddSingleton(connectedBots);

            // Register Bots repository as a singleton service
            builder.Services.AddSingleton<IBotsRepository, BotsRepository>();

            // Register Copilot repository as a singleton service
            builder.Services.AddSingleton<IMcpRepository, McpRepository>();

            // Register open AI client as a singleton service implementing IOpenAiClient interface
            builder.Services.AddSingleton<IOpenAiClient, OpenAiClient>();

            // Register SVG cache model as a singleton service
            builder.Services.AddSingleton(new SvgCacheModel
            {
                Svgs = ControllerUtilities.ReadSvgs(wwwrootPath)
            });

            // Register OpenAI tools repository as a singleton service implementing IToolsRepository interface
            builder.Services.AddSingleton<IToolsRepository, ToolsRepository>();

            // Register various adapters as transient services for dependency injection
            builder.Services.AddTransient<AspAdapter>();
            builder.Services.AddTransient<G4Adapter>();
            builder.Services.AddTransient<HubsAdapter>();
            builder.Services.AddTransient<ResourcesAdapter>();

            // Register the G4Domain as a transient service implementing IDomain
            builder.Services.AddTransient<IDomain, G4Domain>();
        }

        // Creates a new instance of G4Client and sets up event handlers for automation notifications.
        private static G4Client NewG4Client(IServiceProvider serviceProvider, CacheManager cache)
        {
            // Get the queue manager instance from the service provider.
            var queueManager = serviceProvider.GetRequiredService<IQueueManager>();

            // Get the logger instance from the service provider.
            var logger = serviceProvider.GetRequiredService<ILogger>();

            // Initialize the G4Client with the provided cache manager.
            var client = new G4Client(cache, queueManager, logger);

            // Initialize the SignalR notifications hub context from the service provider.
            InitializeSyncClient(client, serviceProvider);

            // Initialize the asynchronous SignalR G4 hub context from the service provider.
            InitializeAsyncClient(client, serviceProvider);

            // Return the fully configured client instance.
            return client;
        }

        // Initializes the event handlers for the asynchronous G4 client.
        private static void InitializeAsyncClient(G4Client client, IServiceProvider serviceProvider)
        {
            // Resolve the SignalR notifications hub from the service provider.
            var context = serviceProvider.GetRequiredService<IHubContext<G4Hub>>();

            // Set up the event handler for when an automation is completed.
            client.AutomationAsync.AutomationInvoked += (sender, e) =>
            {
                // Retrieve the connection ID for SignalR from environment variables.
                var connectionId = e.Automation.GetConnection();

                // Send a notification about the completion of the automation to the specified SignalR client.
                context.SendMessage(connectionId, method: "ReceiveAutomationInvokedEvent", message: new EventDataModel
                {
                    Id = e.Automation.Reference.Id,
                    ObjectType = nameof(G4AutomationModel),
                    Type = "Automation",
                    Value = new
                    {
                        Request = e.Automation,
                        e.Response
                    }
                });
            };
        }

        // Initializes the event handlers for the synchronous G4 client.
        private static void InitializeSyncClient(G4Client client, IServiceProvider serviceProvider)
        {
            // Resolve the SignalR notifications hub from the service provider.
            var context = serviceProvider.GetRequiredService<IHubContext<G4AutomationNotificationsHub>>();

            // Event handler for when an automation has finished executing.
            client.Automation.AutomationInvoked += (sender, e) =>
            {
                // Retrieve the SignalR connection ID tied to this automation.
                var connectionId = e.Automation.GetConnection();

                // Notify the client about the automation completion (with request and response data).
                context.SendMessage(connectionId, method: "ReceiveAutomationInvokedEvent", message: new EventDataModel
                {
                    Id = e.Automation.Reference.Id,
                    ObjectType = nameof(G4AutomationModel),
                    Type = "Automation",
                    Value = new
                    {
                        Request = e.Automation,
                        e.Response
                    }
                });
            };

            // Event handler for when an automation is starting.
            client.Automation.AutomationInvoking += (sender, e) =>
            {
                // Retrieve the SignalR connection ID tied to this automation.
                var connectionId = e.Automation.GetConnection();

                // Notify the client about the automation start.
                context.SendMessage(connectionId, method: "ReceiveAutomationStartEvent", message: new EventDataModel
                {
                    Id = e.Automation.Reference.Id,
                    ObjectType = nameof(G4AutomationModel),
                    Type = "Automation",
                    Value = e.Automation
                });
            };

            // Event handler for when a stage has finished executing.
            client.Automation.StageInvoked += (sender, e) =>
            {
                // Retrieve the SignalR connection ID tied to this automation.
                var connectionId = e.Automation.GetConnection();

                // Notify the client about the stage completion.
                context.SendMessage(connectionId, method: "ReceiveAutomationEndEvent", message: new EventDataModel
                {
                    Id = e.Stage.Reference.Id,
                    ObjectType = nameof(G4StageModel),
                    Type = "Stage",
                    Value = e.Stage
                });
            };

            // Event handler for when a stage is starting.
            client.Automation.StageInvoking += (sender, e) =>
            {
                // Retrieve the SignalR connection ID tied to this automation.
                var connectionId = e.Automation.GetConnection();

                // Notify the client about the stage start.
                context.SendMessage(connectionId, method: "ReceiveAutomationStartEvent", message: new EventDataModel
                {
                    Id = e.Stage.Reference.Id,
                    ObjectType = nameof(G4StageModel),
                    Type = "Stage",
                    Value = e.Stage
                });
            };

            // Event handler for when a job has finished executing.
            client.Automation.JobInvoked += (sender, e) =>
            {
                // Retrieve the SignalR connection ID tied to this automation.
                var connectionId = e.Automation.GetConnection();

                // Notify the client about the job completion.
                context.SendMessage(connectionId, method: "ReceiveAutomationEndEvent", message: new EventDataModel
                {
                    Id = e.Job.Reference.Id,
                    ObjectType = nameof(G4JobModel),
                    Type = "Job",
                    Value = e.Job
                });
            };

            // Event handler for when a job is starting.
            client.Automation.JobInvoking += (sender, e) =>
            {
                // Retrieve the SignalR connection ID tied to this automation.
                var connectionId = e.Automation.GetConnection();

                // Notify the client about the job start.
                context.SendMessage(connectionId, method: "ReceiveAutomationStartEvent", message: new EventDataModel
                {
                    Id = e.Job.Reference.Id,
                    ObjectType = nameof(G4JobModel),
                    Type = "Job",
                    Value = e.Job
                });
            };

            // Event handler for when a rule has finished executing.
            client.Automation.RuleInvoked += (sender, e) =>
            {
                // Retrieve the SignalR connection ID tied to this automation.
                var connectionId = e.Automation.GetConnection();

                // Notify the client about the rule completion (with results, extractions, and exceptions).
                context.SendMessage(connectionId, method: "ReceiveAutomationEndEvent", message: new EventDataModel
                {
                    Id = e.Rule.Reference.Id,
                    ObjectType = e.Rule.GetType().Name,
                    Type = e.Rule.GetManifest()?.PluginType ?? "SystemPlugin",
                    Value = new
                    {
                        e.Rule,
                        e.Extractions,
                        e.Exceptions
                    }
                });
            };

            // Event handler for when a rule is starting.
            client.Automation.RuleInvoking += (sender, e) =>
            {
                // Retrieve the SignalR connection ID tied to this automation.
                var connectionId = e.Automation.GetConnection();

                // Notify the client about the rule start.
                context.SendMessage(connectionId, method: "ReceiveAutomationStartEvent", message: new EventDataModel
                {
                    Id = e.Rule.Reference.Id,
                    ObjectType = e.Rule.GetType().Name,
                    Type = e.Rule.GetManifest()?.PluginType ?? "SystemPlugin",
                    Value = e.Rule
                });
            };

            // Event handler for when an automation request has been initialized.
            client.Automation.AutomationRequestInitialized += (_, e) =>
            {
                // Retrieve the SignalR connection ID tied to this automation.
                var connectionId = e.Status.Automation.GetConnection();

                // Notify the client about the automation request initialization.
                context.SendMessage(connectionId, method: "ReceiveAutomationRequestInitializedEvent", message: new EventDataModel
                {
                    Id = e.Status.Automation.Reference.Id,
                    ObjectType = nameof(G4AutomationModel),
                    Type = "Automation",
                    Value = e.Status.Automation
                });
            };

            // Event handler for when a log entry is about to be created.
            client.Automation.LogCreating += (_, e) =>
            {
                // Retrieve the SignalR connection ID tied to this automation.
                var connectionId = e.Automation.GetConnection();

                // Notify the client about the pending log creation.
                context.SendMessage(connectionId, method: "ReceiveLogCreatingEvent", message: new EventDataModel
                {
                    Id = e.Invoker,
                    ObjectType = nameof(LogEventArgs),
                    Type = "Log",
                    Value = e.LogMessage
                });
            };

            // Event handler for when a log entry has been created.
            client.Automation.LogCreated += (_, e) =>
            {
                // Retrieve the SignalR connection ID tied to this automation.
                var connectionId = e.Automation.GetConnection();

                // Notify the client about the completed log entry.
                context.SendMessage(connectionId, method: "ReceiveLogCreatedEvent", message: new EventDataModel
                {
                    Id = e.Invoker,
                    ObjectType = nameof(LogEventArgs),
                    Type = "Log",
                    Value = e.LogMessage
                });
            };
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

            /// <summary>
            /// Gets or sets the name of the underlying class of the entity being used.
            /// </summary>
            public string ObjectType { get; set; }

            /// <summary>
            /// Gets or sets the expressive entity type, as defined in the plugin manifest
            /// or hardcoded in the application.
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// Gets or sets the complete state of the entity at the time of the event.
            /// </summary>
            public object Value { get; set; }
        }
        #endregion
    }
}
