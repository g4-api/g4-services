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
    /// Optional parameters carry default values because the MCP schema marks a parameter optional only when it has one.
    /// </remarks>
    /// <param name="tools">
    /// The singleton catalog and session owner; its lifetime belongs to the application host.
    /// </param>
    [McpServerToolType]
    public sealed class G4SystemTools(IToolsRepository tools)
    {
        #region *** Constants    ***

        // Shared description of the browser session argument used by the rule execution tools.
        private const string DriverSessionDescription = "The unique session ID associated with the current browser " +
            "session. This ID is used to retrieve the appropriate browser driver for interacting with the session " +
            "and performing automation tasks.";

        // Shared description of the G4 authentication token argument used by the session-bound tools.
        private const string TokenDescription = "The G4 Authentication token used to authenticate the session " +
            "initiation process. This is required to authorize the session creation.";

        #endregion

        #region *** Methods      ***

        /// <summary>
        /// Searches the cached example library for rule examples that match a tool name and intent.
        /// </summary>
        /// <param name="intent">The user and agent intent used to search and rank examples.</param>
        /// <param name="maxResults">The maximum number of examples to return.</param>
        /// <param name="namespace">The plugin namespace that scopes the search.</param>
        /// <param name="toolName">The tool whose examples are searched.</param>
        /// <param name="threshold">The optional minimum relevance score of a returned example.</param>
        /// <returns>
        /// The scored examples as JSON text and structured content, or a tool error for an invalid query.
        /// </returns>
        [McpServerTool(Name = "g4.FindExamples", Title = "Find G4 Examples", ReadOnly = true, Idempotent = true)]
        [Description("Searches the cached example library for rule examples that match the supplied tool name and " +
            "intent, returning each example with its relevance score, exported rule properties, and parsed CLI " +
            "parameters.")]
        public Task<CallToolResult> FindExamplesAsync(
            [Description("The user and agent intent used to search and rank matching examples.")]
            ToolIntentModel intent,
            [Description("The maximum number of examples to return in the search results.")] int maxResults,
            [Description("A namespace used to scope the example search to a specific plugin group or category.")]
            string @namespace,
            [Description("The unique identifier of the tool for which examples are being searched.")] string toolName,
            [Description("The minimum relevance score required for an example to be included in the results.")]
            int? threshold = null)
        {
            // Forward the documented query fields; the repository deserializes them into its example query model.
            var arguments = new { intent, maxResults, @namespace, threshold, toolName };
            return G4ToolsInvoker.InvokeAsync(tools, name: "g4.FindExamples", arguments);
        }

        /// <summary>
        /// Retrieves the complete G4 tool model for an exact tool name.
        /// </summary>
        /// <param name="toolName">The exact or qualified name of the tool to find.</param>
        /// <returns>The tool model as JSON text and structured content, or JSON null when no tool matches.</returns>
        [McpServerTool(Name = "g4.FindTool", Title = "Find G4 Tool", ReadOnly = true, Idempotent = true)]
        [Description("Retrieves the complete G4 tool model for an exact tool name, including its authoritative " +
            "namespace and client tool schema.")]
        public Task<CallToolResult> FindToolAsync(
            [Description("The unique identifier of the tool to find.")] string toolName)
        {
            // Forward the name; the repository matches it against tool names first and qualified names second.
            return G4ToolsInvoker.InvokeAsync(tools, name: "g4.FindTool", arguments: new { toolName });
        }

        /// <summary>
        /// Retrieves the tools most relevant to an intent through lexical retrieval.
        /// </summary>
        /// <param name="intent">The user and agent intent used to rank the tools.</param>
        /// <param name="take">The optional maximum number of tools; the repository defaults to three.</param>
        /// <returns>The ranked tools as JSON text and structured content.</returns>
        [McpServerTool(Name = "g4.FindTools", Title = "Find G4 Tools", ReadOnly = true, Idempotent = true)]
        [Description("Retrieves the full list of available tools that the AI agent can invoke.")]
        public Task<CallToolResult> FindToolsAsync(
            [Description("The user and agent intent used to discover and rank the most relevant tools for the " +
                "current task.")] ToolIntentModel intent,
            [Description("The maximum number of tools to return.")] int? take = null)
        {
            // Forward the intent and limit; the repository validates the limit and applies its default.
            var arguments = new { intent, take };
            return G4ToolsInvoker.InvokeAsync(tools, name: "g4.FindTools", arguments);
        }

        /// <summary>
        /// Retrieves the buffered rule execution entries of an active session.
        /// </summary>
        /// <param name="sessionId">The session whose buffer is read.</param>
        /// <returns>The buffered entries as JSON text and structured content.</returns>
        [McpServerTool(Name = "g4.GetBuffer", Title = "Get G4 Rules Buffer", ReadOnly = true, Idempotent = true)]
        [Description("Retrieves the buffered rule execution entries for an active G4 session.")]
        public Task<CallToolResult> GetBufferAsync(
            [Description("The unique identifier of the active G4 session whose buffered rules should be retrieved.")]
            string sessionId)
        {
            // Read the buffer owned by the repository; the session identifier is the only argument.
            return G4ToolsInvoker.InvokeAsync(tools, name: "g4.GetBuffer", arguments: new { sessionId });
        }

        /// <summary>
        /// Registers a promoted flow-template capability as a discoverable, callable tool.
        /// </summary>
        /// <param name="manifest">The complete G4 plugin manifest of the capability.</param>
        /// <returns>The registration result as JSON text and structured content.</returns>
        [McpServerTool(Name = "g4.RegisterCapability", Title = "Register G4 Capability", Destructive = false)]
        [Description("Registers a promoted G4 flow-template capability as a discoverable, callable tool. Accepts a " +
            "full G4 plugin manifest (the same shape produced by the flow-to-template promotion pipeline) and " +
            "persists it through the G4 engine's Templates store.")]
        public Task<CallToolResult> RegisterCapabilityAsync(
            [Description("The full G4 plugin manifest describing the capability to register, matching the " +
                "G4PluginAttribute JSON shape (key, pluginType, source, summary, description, parameters, " +
                "properties, rules, context, etc.), the same manifest shape accepted by PUT /api/v4/g4/templates.")]
            Dictionary<string, object> manifest)
        {
            // Forward the manifest unchanged; the repository deserializes it into the plugin attribute shape.
            return G4ToolsInvoker.InvokeAsync(tools, name: "g4.RegisterCapability", arguments: new { manifest });
        }

        /// <summary>
        /// Removes the rule execution buffer of a session.
        /// </summary>
        /// <param name="sessionId">The session whose buffer is removed.</param>
        /// <returns>The removal result as JSON text and structured content.</returns>
        [McpServerTool(
            Name = "g4.RemoveBuffer", Title = "Remove G4 Rules Buffer", Destructive = true, Idempotent = true)]
        [Description("Removes the rule execution buffer associated with a G4 session, releasing its resources.")]
        public Task<CallToolResult> RemoveBufferAsync(
            [Description("The unique identifier of the G4 session whose buffer should be removed.")] string sessionId)
        {
            // Release the buffer owned by the repository; the session identifier is the only argument.
            return G4ToolsInvoker.InvokeAsync(tools, name: "g4.RemoveBuffer", arguments: new { sessionId });
        }

        /// <summary>
        /// Removes an active session from the sessions registry.
        /// </summary>
        /// <param name="sessionId">The session to remove.</param>
        /// <returns>The removal result as JSON text and structured content.</returns>
        [McpServerTool(Name = "g4.RemoveSession", Title = "Remove G4 Session", Destructive = true, Idempotent = true)]
        [Description("Removes an active G4 session from the sessions registry, releasing its resources.")]
        public Task<CallToolResult> RemoveSessionAsync(
            [Description("The unique identifier of the G4 session to remove.")] string sessionId)
        {
            // Release the session owned by the repository; the session identifier is the only argument.
            return G4ToolsInvoker.InvokeAsync(tools, name: "g4.RemoveSession", arguments: new { sessionId });
        }

        /// <summary>
        /// Resolves a locator for the element that an intent describes on the current page.
        /// </summary>
        /// <param name="driverSession">The browser session in which the element is searched.</param>
        /// <param name="intent">The user and agent intent describing the element.</param>
        /// <param name="openaiApiKey">The OpenAI key that authorizes the locator request.</param>
        /// <param name="openaiModel">The OpenAI model that ranks locator candidates.</param>
        /// <param name="openaiUri">The OpenAI endpoint that receives the locator request.</param>
        /// <param name="token">The G4 authentication token that authorizes the DOM retrieval.</param>
        /// <param name="constraints">The optional locator-selection rules.</param>
        /// <returns>The resolved locator as JSON text and structured content.</returns>
        [McpServerTool(Name = "g4.ResolveLocator", Title = "Resolve Element Locator", ReadOnly = true)]
        [Description("Retrieves a locator for a specific element on the page based on the provided parameters.")]
        public Task<CallToolResult> ResolveLocatorAsync(
            [Description("Unique identifier for the active browser session in which the locator search is performed.")]
            string driverSession,
            [Description("The user and agent intent describing the element to locate on the page, used to guide " +
                "locator strategy selection.")] ToolIntentModel intent,
            [Description("OpenAI authentication token for verifying and authorizing the locator retrieval request.")]
            string openaiApiKey,
            [Description("Specifies the OpenAI model identifier (e.g., 'gpt-4o', 'gpt-4.1-mini').")]
            string openaiModel,
            [Description("OpenAI API endpoint URI for the request.")] string openaiUri,
            [Description("G4 authentication token for verifying and authorizing the locator retrieval request.")]
            string token,
            [Description("Optional rules influencing how the locator is selected, such as visibility, enablement, " +
                "and allowed/disallowed strategies.")] LocatorConstraintsModel constraints = null)
        {
            // Forward the session, intent, model settings, and constraints under their published names.
            var arguments = new { constraints, driverSession, intent, openaiApiKey, openaiModel, openaiUri, token };
            return G4ToolsInvoker.InvokeAsync(tools, name: "g4.ResolveLocator", arguments);
        }

        /// <summary>
        /// Executes one plugin rule in an existing browser session.
        /// </summary>
        /// <param name="driverSession">The browser session that executes the rule.</param>
        /// <param name="intent">The user and agent intent describing the rule's purpose.</param>
        /// <param name="token">The G4 authentication token that authorizes the execution.</param>
        /// <param name="rule">The plugin rule to execute.</param>
        /// <returns>The execution result as JSON text and structured content.</returns>
        [McpServerTool(Name = "g4.SendRule", Title = "Send G4 Rule", Idempotent = false)]
        [Description("Sends a G4 rule for execution with the provided parameters.")]
        public Task<CallToolResult> SendRuleAsync(
            [Description(DriverSessionDescription)] string driverSession,
            [Description("The user and agent intent describing what the rule is meant to accomplish during execution.")]
            ToolIntentModel intent,
            [Description(TokenDescription)] string token,
            [Description("The G4 rule to be executed, including its parameters and configuration.")]
            ToolRuleModel rule)
        {
            // Forward the rule with its session context; the repository converts it into an executable rule.
            var arguments = new { driverSession, intent, rule, token };
            return G4ToolsInvoker.InvokeAsync(tools, name: "g4.SendRule", arguments);
        }

        /// <summary>
        /// Executes an ordered collection of plugin rules as one job in an existing browser session.
        /// </summary>
        /// <param name="driverSession">The browser session that executes the rules.</param>
        /// <param name="intent">The user and agent intent describing the rules' purpose.</param>
        /// <param name="token">The G4 authentication token that authorizes the execution.</param>
        /// <param name="rules">The ordered plugin rules to execute.</param>
        /// <returns>The execution result as JSON text and structured content.</returns>
        [McpServerTool(Name = "g4.SendRules", Title = "Send G4 Rules", Idempotent = false)]
        [Description("Sends a collection of G4 rules for sequential execution within a single driver session.")]
        public Task<CallToolResult> SendRulesAsync(
            [Description(DriverSessionDescription)] string driverSession,
            [Description("The user and agent intent describing what the rules are meant to accomplish during " +
                "execution.")]
            ToolIntentModel intent,
            [Description(TokenDescription)] string token,
            [Description("The ordered collection of G4 rules to be executed as one job, including their parameters " +
                "and configuration.")] ToolRuleModel[] rules)
        {
            // Forward the ordered rules with their session context; the repository runs them as one job.
            var arguments = new { driverSession, intent, rules, token };
            return G4ToolsInvoker.InvokeAsync(tools, name: "g4.SendRules", arguments);
        }

        /// <summary>
        /// Starts a new browser automation session.
        /// </summary>
        /// <param name="driver">The browser driver name.</param>
        /// <param name="driverBinaries">The driver executable path or Selenium Grid endpoint.</param>
        /// <param name="token">The G4 authentication token that authorizes the session.</param>
        /// <param name="capabilities">The optional W3C capabilities applied to every match.</param>
        /// <param name="firstMatch">The optional ordered W3C capability fallbacks.</param>
        /// <returns>The new driver session identifier as JSON text and structured content.</returns>
        [McpServerTool(Name = "g4.StartSession", Title = "Start G4 Session", Destructive = false, Idempotent = false)]
        [Description("Starts a new G4 session using specified driver binaries, browser (platform) name, and " +
            "headless option.")]
        public Task<CallToolResult> StartSessionAsync(
            [Description("The name of the browser driver to use (e.g., ChromeDriver, GeckoDriver). This is " +
                "necessary for the automation to interact with the browser.")] string driver,
            [Description("The path to the browser driver executable (e.g., ChromeDriver) or the URL of the " +
                "Selenium Grid endpoint. This is necessary for the automation to interact with the browser.")]
            string driverBinaries,
            [Description(TokenDescription)] string token,
            [Description("W3C WebDriver capabilities object applied to every session match (alwaysMatch). Use to " +
                "set browser name, platform, or vendor-specific options such as headless mode, window size, or " +
                "custom browser arguments.")] Dictionary<string, object> capabilities = null,
            [Description("An ordered list of W3C WebDriver capability objects. The driver selects the first set it " +
                "can satisfy, allowing fallback configurations across browsers or environments.")]
            Dictionary<string, object>[] firstMatch = null)
        {
            // Forward every argument; the repository passes the complete set to the driver as driver parameters.
            var arguments = new { capabilities, driver, driverBinaries, firstMatch, token };
            return G4ToolsInvoker.InvokeAsync(tools, name: "g4.StartSession", arguments);
        }

        #endregion
    }
}
