using G4.Services.Domain.V4.Repositories;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Mcp
{
    /// <summary>
    /// Exposes the built-in G4 system tools to MCP clients through typed, attribute-declared tool methods.
    /// </summary>
    /// <remarks>
    /// The SDK generates every input schema from the method parameters and lists these tools together with
    /// <see cref="RecordingTools"/>. Tool names, argument names, and result fields keep the published wire contract:
    /// each method rebuilds the original name-and-arguments call and executes it through
    /// <see cref="IToolsRepository.CallTool"/>, so the repository behavior is identical for every transport.
    /// Every tool name, title, and description is read from the nested <c>Catalog</c>, so the methods carry behavior
    /// only. Optional parameters carry default values because the MCP schema marks a parameter optional only when it
    /// has one.
    /// </remarks>
    /// <param name="tools">The singleton catalog and session owner; its lifetime belongs to the application host.</param>
    [McpServerToolType]
    public sealed class G4SystemTools(IToolsRepository tools)
    {
        #region *** Methods      ***

        [McpServerTool(
            Name = Catalog.FindExamples.Name, Title = Catalog.FindExamples.Title, ReadOnly = true, Idempotent = true)]
        [Description(Catalog.FindExamples.Description)]
        public Task<CallToolResult> FindExamplesAsync(
            [Description(Catalog.FindExamples.Arguments.Intent)] ToolIntentModel intent,
            [Description(Catalog.FindExamples.Arguments.MaxResults)] int maxResults,
            [Description(Catalog.FindExamples.Arguments.Namespace)] string @namespace,
            [Description(Catalog.FindExamples.Arguments.ToolName)] string toolName,
            [Description(Catalog.FindExamples.Arguments.Threshold)] int? threshold = null)
        {
            // Set the arguments under their published names;
            // the repository validates the limit and applies its default.
            var arguments = new
            {
                intent,
                maxResults,
                @namespace,
                threshold,
                toolName
            };

            // Forward the query to the tools repository;
            // the repository ranks the examples and returns them with their relevance scores.
            return G4ToolsInvoker.InvokeAsync(tools, name: Catalog.FindExamples.Name, arguments);
        }

        [McpServerTool(
            Name = Catalog.FindTool.Name, Title = Catalog.FindTool.Title, ReadOnly = true, Idempotent = true)]
        [Description(Catalog.FindTool.Description)]
        public Task<CallToolResult> FindToolAsync([Description(Catalog.FindTool.Arguments.ToolName)] string toolName)
        {
            // Forward the name;
            // the repository resolves a qualified Namespace:Name or a bare name, as the engine does.
            return G4ToolsInvoker.InvokeAsync(tools, name: Catalog.FindTool.Name, arguments: new { toolName });
        }

        [McpServerTool(
            Name = Catalog.FindTools.Name, Title = Catalog.FindTools.Title, ReadOnly = true, Idempotent = true)]
        [Description(Catalog.FindTools.Description)]
        public Task<CallToolResult> FindToolsAsync(
            [Description(Catalog.FindTools.Arguments.Intent)] ToolIntentModel intent,
            [Description(Catalog.FindTools.Arguments.Take)] int? take = null)
        {
            // Set the arguments under their published names;
            // the repository validates the limit and applies its default.
            var arguments = new
            {
                intent,
                take
            };

            // Forward the query to the tools repository;
            // the repository ranks the tools and returns them with their relevance scores.
            return G4ToolsInvoker.InvokeAsync(tools, name: Catalog.FindTools.Name, arguments);
        }

        [McpServerTool(
            Name = Catalog.GetBuffer.Name, Title = Catalog.GetBuffer.Title, ReadOnly = true, Idempotent = true)]
        [Description(Catalog.GetBuffer.Description)]
        public Task<CallToolResult> GetBufferAsync(
            [Description(Catalog.GetBuffer.Arguments.SessionId)] string sessionId)
        {
            // Read the buffer owned by the repository;
            // the session identifier is the only argument.
            return G4ToolsInvoker.InvokeAsync(tools, name: Catalog.GetBuffer.Name, arguments: new { sessionId });
        }

        [McpServerTool(
            Name = Catalog.RegisterCapability.Name, Title = Catalog.RegisterCapability.Title, Destructive = false)]
        [Description(Catalog.RegisterCapability.Description)]
        public Task<CallToolResult> RegisterCapabilityAsync(
            [Description(Catalog.RegisterCapability.Arguments.Manifest)] Dictionary<string, object> manifest)
        {
            // Forward the manifest unchanged;
            // the repository deserializes it into the plugin attribute shape.
            var arguments = new { manifest };
            return G4ToolsInvoker.InvokeAsync(tools, name: Catalog.RegisterCapability.Name, arguments);
        }

        [McpServerTool(
            Name = Catalog.RemoveBuffer.Name,
            Title = Catalog.RemoveBuffer.Title,
            Destructive = true,
            Idempotent = true)]
        [Description(Catalog.RemoveBuffer.Description)]
        public Task<CallToolResult> RemoveBufferAsync(
            [Description(Catalog.RemoveBuffer.Arguments.SessionId)] string sessionId)
        {
            // Release the buffer owned by the repository;
            // the session identifier is the only argument.
            return G4ToolsInvoker.InvokeAsync(tools, name: Catalog.RemoveBuffer.Name, arguments: new { sessionId });
        }

        [McpServerTool(
            Name = Catalog.RemoveSession.Name,
            Title = Catalog.RemoveSession.Title,
            Destructive = true,
            Idempotent = true)]
        [Description(Catalog.RemoveSession.Description)]
        public Task<CallToolResult> RemoveSessionAsync(
            [Description(Catalog.RemoveSession.Arguments.SessionId)] string sessionId)
        {
            // Release the session owned by the repository;
            // the session identifier is the only argument.
            return G4ToolsInvoker.InvokeAsync(tools, name: Catalog.RemoveSession.Name, arguments: new { sessionId });
        }

        [McpServerTool(Name = Catalog.ResolveLocator.Name, Title = Catalog.ResolveLocator.Title, ReadOnly = true)]
        [Description(Catalog.ResolveLocator.Description)]
        public Task<CallToolResult> ResolveLocatorAsync(
            [Description(Catalog.ResolveLocator.Arguments.DriverSession)] string driverSession,
            [Description(Catalog.ResolveLocator.Arguments.Intent)] ToolIntentModel intent,
            [Description(Catalog.ResolveLocator.Arguments.OpenaiApiKey)] string openaiApiKey,
            [Description(Catalog.ResolveLocator.Arguments.OpenaiModel)] string openaiModel,
            [Description(Catalog.ResolveLocator.Arguments.OpenaiUri)] string openaiUri,
            [Description(Catalog.ResolveLocator.Arguments.Token)] string token,
            [Description(Catalog.ResolveLocator.Arguments.Constraints)] LocatorConstraintsModel constraints = null)
        {
            // Set the arguments under their published names;
            // the repository validates the constraints and applies defaults.
            var arguments = new
            {
                constraints,
                driverSession,
                intent,
                openaiApiKey,
                openaiModel,
                openaiUri,
                token
            };

            // Forward the query to the tools repository;
            // the repository resolves the locator and returns it with its strategy.
            return G4ToolsInvoker.InvokeAsync(tools, name: Catalog.ResolveLocator.Name, arguments);
        }

        [McpServerTool(
            Name = Catalog.ResolveTools.Name, Title = Catalog.ResolveTools.Title, ReadOnly = true, Idempotent = true)]
        [Description(Catalog.ResolveTools.Description)]
        public Task<CallToolResult> ResolveToolsAsync(
            [Description(Catalog.ResolveTools.Arguments.Intent)] ToolIntentModel intent,
            [Description(Catalog.ResolveTools.Arguments.Take)] int? take = null,
            [Description(Catalog.ResolveTools.Arguments.ExamplesTake)] int? examplesTake = null)
        {
            // Set the arguments under their published names;
            // the repository validates both limits and applies their defaults.
            var arguments = new
            {
                examplesTake,
                intent,
                take
            };

            // Forward the query to the tools repository;
            // the repository ranks the tools and their examples by the same intent.
            return G4ToolsInvoker.InvokeAsync(tools, name: Catalog.ResolveTools.Name, arguments);
        }

        [McpServerTool(Name = Catalog.SendRule.Name, Title = Catalog.SendRule.Title, Idempotent = false)]
        [Description(Catalog.SendRule.Description)]
        public Task<CallToolResult> SendRuleAsync(
            [Description(Catalog.SendRule.Arguments.DriverSession)] string driverSession,
            [Description(Catalog.SendRule.Arguments.Intent)] ToolIntentModel intent,
            [Description(Catalog.SendRule.Arguments.Token)] string token,
            [Description(Catalog.SendRule.Arguments.Rule)] ToolRuleModel rule)
        {
            // Set the arguments under their published names;
            // the repository validates the rule and applies its defaults.
            var arguments = new
            {
                driverSession,
                intent,
                rule,
                token
            };

            // Forward the rule to the tools repository; the repository runs it as one job.
            return G4ToolsInvoker.InvokeAsync(tools, name: Catalog.SendRule.Name, arguments);
        }

        [McpServerTool(Name = Catalog.SendRules.Name, Title = Catalog.SendRules.Title, Idempotent = false)]
        [Description(Catalog.SendRules.Description)]
        public Task<CallToolResult> SendRulesAsync(
            [Description(Catalog.SendRules.Arguments.DriverSession)] string driverSession,
            [Description(Catalog.SendRules.Arguments.Intent)] ToolIntentModel intent,
            [Description(Catalog.SendRules.Arguments.Token)] string token,
            [Description(Catalog.SendRules.Arguments.Rules)] ToolRuleModel[] rules)
        {
            // Set the arguments under their published names;
            // the repository validates the rules and applies their defaults.
            var arguments = new { driverSession, intent, rules, token };

            // Forward the rules to the tools repository;
            // the repository runs them as one job.
            return G4ToolsInvoker.InvokeAsync(tools, name: Catalog.SendRules.Name, arguments);
        }

        [McpServerTool(
            Name = Catalog.StartSession.Name,
            Title = Catalog.StartSession.Title,
            Destructive = false,
            Idempotent = false)]
        [Description(Catalog.StartSession.Description)]
        public Task<CallToolResult> StartSessionAsync(
            [Description(Catalog.StartSession.Arguments.Driver)] string driver,
            [Description(Catalog.StartSession.Arguments.DriverBinaries)] string driverBinaries,
            [Description(Catalog.StartSession.Arguments.Token)] string token,
            [Description(Catalog.StartSession.Arguments.Capabilities)] Dictionary<string, object> capabilities = null,
            [Description(Catalog.StartSession.Arguments.FirstMatch)] Dictionary<string, object>[] firstMatch = null)
        {
            // Set the arguments under their published names;
            // the repository validates the driver and capabilities and applies defaults.
            var arguments = new
            {
                capabilities,
                driver,
                driverBinaries,
                firstMatch,
                token
            };

            // Forward the session request to the tools repository;
            // the repository starts the session and returns its ID.
            return G4ToolsInvoker.InvokeAsync(tools, name: Catalog.StartSession.Name, arguments);
        }

        #endregion

        #region *** Nested Types ***

        // Holds the published wording of every system tool, one nested class per tool in method order. Each tool class
        // exposes Name, Title, and Description, plus a nested Arguments class with one constant per argument named
        // after the argument in PascalCase, so a method reads every text as Catalog.<Tool>.<Member> or
        // Catalog.<Tool>.Arguments.<Argument>. Arguments is nested because argument names such as description or
        // summary would otherwise collide with the tool members. The text is wire contract: MCP clients receive it in
        // tools/list, g4.FindTool, and g4.FindTools, and agents choose and fill tools from it.
        private static class Catalog
        {
            // Holds argument wording reused by several tools. Tool classes alias these constants instead of copying
            // the text, so one edit changes every tool that shares the argument.
            private static class Common
            {
                #region *** Constants    ***

                /// <summary>
                /// Describes the browser session argument of the rule execution tools.
                /// </summary>
                public const string DriverSession = "The unique session ID associated with the current browser " +
                    "session. This ID is used to retrieve the appropriate browser driver for interacting with the " +
                    "session and performing automation tasks.";

                /// <summary>
                /// Describes the tool count limit of the tool ranking tools.
                /// </summary>
                public const string Take = "The maximum number of tools to return.";

                /// <summary>
                /// Describes the G4 authentication token argument of the session-bound tools.
                /// </summary>
                public const string Token = "The {product} Authentication token used to authenticate the session initiation " +
                    "process. This is required to authorize the session creation.";

                #endregion
            }

            // Wording of the g4.FindExamples tool.
            public static class FindExamples
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.FindExamples;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Find {product} Examples";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Searches the cached example library for rule examples that match " +
                    "the supplied tool name and intent, returning each example with its relevance score, exported " +
                    "rule properties, and parsed CLI parameters.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the intent argument.
                    /// </summary>
                    public const string Intent = "The user and agent intent used to search and rank matching examples.";

                    /// <summary>
                    /// Describes the maximum results argument.
                    /// </summary>
                    public const string MaxResults = "The maximum number of examples to return in the search results.";

                    /// <summary>
                    /// Describes the namespace argument.
                    /// </summary>
                    public const string Namespace = "A namespace used to scope the example search to a specific " +
                        "plugin group or category.";

                    /// <summary>
                    /// Describes the relevance threshold argument.
                    /// </summary>
                    public const string Threshold = "The minimum relevance score required for an example to be " +
                        "included in the results.";

                    /// <summary>
                    /// Describes the tool name argument.
                    /// </summary>
                    public const string ToolName = "The unique identifier of the tool for which examples are being " +
                        "searched.";

                    #endregion
                }

                #endregion
            }

            // Wording of the g4.FindTool tool.
            public static class FindTool
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.FindTool;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Find {product} Tool";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Retrieves the complete {product} tool model for an exact tool name or " +
                    "its qualified Namespace:Name, including its authoritative namespace and client tool schema.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the tool name argument.
                    /// </summary>
                    public const string ToolName = "The name of the tool to find: its bare name, or its qualified " +
                        "Namespace:Name when several namespaces provide the same name.";

                    #endregion
                }

                #endregion
            }

            // Wording of the g4.FindTools tool.
            public static class FindTools
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.FindTools;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Find {product} Tools";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Retrieves the full list of available tools that the AI agent can " +
                    "invoke.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the intent argument.
                    /// </summary>
                    public const string Intent = "The user and agent intent used to discover and rank the most " +
                        "relevant tools for the current task.";

                    /// <summary>
                    /// Describes the tool count limit argument.
                    /// </summary>
                    public const string Take = Common.Take;

                    #endregion
                }

                #endregion
            }

            // Wording of the g4.GetBuffer tool.
            public static class GetBuffer
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.GetBuffer;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Get {product} Rules Buffer";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Retrieves the buffered rule execution entries for an active {product} " +
                    "session.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the session identifier argument.
                    /// </summary>
                    public const string SessionId = "The unique identifier of the active {product} session whose buffered " +
                        "rules should be retrieved.";

                    #endregion
                }

                #endregion
            }

            // Wording of the g4.RegisterCapability tool.
            public static class RegisterCapability
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.RegisterCapability;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Register {product} Capability";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Registers a promoted {product} flow-template capability as a " +
                    "discoverable, callable tool. Accepts a full {product} plugin manifest (the same shape produced by the " +
                    "flow-to-template promotion pipeline) and persists it through the {product} engine's Templates store.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the plugin manifest argument.
                    /// </summary>
                    public const string Manifest = "The full {product} plugin manifest describing the capability to " +
                        "register, matching the G4PluginAttribute JSON shape (key, pluginType, source, summary, " +
                        "description, parameters, properties, rules, context, etc.), the same manifest shape " +
                        "accepted by PUT /api/v4/g4/templates.";

                    #endregion
                }

                #endregion
            }

            // Wording of the g4.RemoveBuffer tool.
            public static class RemoveBuffer
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.RemoveBuffer;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Remove {product} Rules Buffer";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Removes the rule execution buffer associated with a {product} session, " +
                    "releasing its resources.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the session identifier argument.
                    /// </summary>
                    public const string SessionId = "The unique identifier of the {product} session whose buffer should be " +
                        "removed.";

                    #endregion
                }

                #endregion
            }

            // Wording of the g4.RemoveSession tool.
            public static class RemoveSession
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.RemoveSession;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Remove {product} Session";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Removes an active {product} session from the sessions registry, " +
                    "releasing its resources.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the session identifier argument.
                    /// </summary>
                    public const string SessionId = "The unique identifier of the {product} session to remove.";

                    #endregion
                }

                #endregion
            }

            // Wording of the g4.ResolveLocator tool. Its session and token arguments keep their own wording because
            // they authorize a locator retrieval, not a session creation.
            public static class ResolveLocator
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.ResolveLocator;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Resolve Element Locator";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Retrieves a locator for a specific element on the page based on " +
                    "the provided parameters.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the locator constraints argument.
                    /// </summary>
                    public const string Constraints = "Optional rules influencing how the locator is selected, such " +
                        "as visibility, enablement, and allowed/disallowed strategies.";

                    /// <summary>
                    /// Describes the browser session argument.
                    /// </summary>
                    public const string DriverSession = "Unique identifier for the active browser session in which " +
                        "the locator search is performed.";

                    /// <summary>
                    /// Describes the intent argument.
                    /// </summary>
                    public const string Intent = "The user and agent intent describing the element to locate on the " +
                        "page, used to guide locator strategy selection.";

                    /// <summary>
                    /// Describes the OpenAI API key argument.
                    /// </summary>
                    public const string OpenaiApiKey = "OpenAI authentication token for verifying and authorizing " +
                        "the locator retrieval request.";

                    /// <summary>
                    /// Describes the OpenAI model argument.
                    /// </summary>
                    public const string OpenaiModel = "Specifies the OpenAI model identifier (e.g., 'gpt-4o', " +
                        "'gpt-4.1-mini').";

                    /// <summary>
                    /// Describes the OpenAI endpoint argument.
                    /// </summary>
                    public const string OpenaiUri = "OpenAI API endpoint URI for the request.";

                    /// <summary>
                    /// Describes the G4 authentication token argument.
                    /// </summary>
                    public const string Token = "{product} authentication token for verifying and authorizing the locator " +
                        "retrieval request.";

                    #endregion
                }

                #endregion
            }

            // Wording of the g4.ResolveTools tool.
            public static class ResolveTools
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.ResolveTools;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Resolve {product} Tools";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Resolves the tools most relevant to the supplied intent in one " +
                    "call, returning each tool's complete model and input schema together with its examples ranked " +
                    "by the same intent.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the per-tool example limit argument.
                    /// </summary>
                    public const string ExamplesTake = "The maximum number of examples to return for each tool.";

                    /// <summary>
                    /// Describes the intent argument.
                    /// </summary>
                    public const string Intent = "The user and agent intent used to rank the most relevant tools and " +
                        "their examples.";

                    /// <summary>
                    /// Describes the tool count limit argument.
                    /// </summary>
                    public const string Take = Common.Take;

                    #endregion
                }

                #endregion
            }

            // Wording of the g4.SendRule tool.
            public static class SendRule
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.SendRule;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Send {product} Rule";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Sends a {product} rule for execution with the provided parameters.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the browser session argument.
                    /// </summary>
                    public const string DriverSession = Common.DriverSession;

                    /// <summary>
                    /// Describes the intent argument.
                    /// </summary>
                    public const string Intent = "The user and agent intent describing what the rule is meant to " +
                        "accomplish during execution.";

                    /// <summary>
                    /// Describes the rule argument.
                    /// </summary>
                    public const string Rule = "The {product} rule to be executed, including its parameters and " +
                        "configuration.";

                    /// <summary>
                    /// Describes the G4 authentication token argument.
                    /// </summary>
                    public const string Token = Common.Token;

                    #endregion
                }

                #endregion
            }

            // Wording of the g4.SendRules tool.
            public static class SendRules
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.SendRules;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Send {product} Rules";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Sends a collection of {product} rules for sequential execution within a " +
                    "single driver session.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the browser session argument.
                    /// </summary>
                    public const string DriverSession = Common.DriverSession;

                    /// <summary>
                    /// Describes the intent argument.
                    /// </summary>
                    public const string Intent = "The user and agent intent describing what the rules are meant to " +
                        "accomplish during execution.";

                    /// <summary>
                    /// Describes the rules argument.
                    /// </summary>
                    public const string Rules = "The ordered collection of {product} rules to be executed as one job, " +
                        "including their parameters and configuration.";

                    /// <summary>
                    /// Describes the G4 authentication token argument.
                    /// </summary>
                    public const string Token = Common.Token;

                    #endregion
                }

                #endregion
            }

            // Wording of the g4.StartSession tool.
            public static class StartSession
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.StartSession;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Start {product} Session";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Starts a new {product} session using specified driver binaries, browser " +
                    "(platform) name, and headless option.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the W3C always-match capabilities argument.
                    /// </summary>
                    public const string Capabilities = "W3C WebDriver capabilities object applied to every session " +
                        "match (alwaysMatch). Use to set browser name, platform, or vendor-specific options such as " +
                        "headless mode, window size, or custom browser arguments.";

                    /// <summary>
                    /// Describes the driver name argument.
                    /// </summary>
                    public const string Driver = "The name of the browser driver to use (e.g., ChromeDriver, " +
                        "GeckoDriver). This is necessary for the automation to interact with the browser.";

                    /// <summary>
                    /// Describes the driver binaries argument.
                    /// </summary>
                    public const string DriverBinaries = "The path to the browser driver executable (e.g., " +
                        "ChromeDriver) or the URL of the Selenium Grid endpoint. This is necessary for the " +
                        "automation to interact with the browser.";

                    /// <summary>
                    /// Describes the W3C first-match capabilities argument.
                    /// </summary>
                    public const string FirstMatch = "An ordered list of W3C WebDriver capability objects. The " +
                        "driver selects the first set it can satisfy, allowing fallback configurations across " +
                        "browsers or environments.";

                    /// <summary>
                    /// Describes the G4 authentication token argument.
                    /// </summary>
                    public const string Token = Common.Token;

                    #endregion
                }

                #endregion
            }
        }

        #endregion
    }
}
