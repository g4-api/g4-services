using G4.Attributes;
using G4.Extensions;
using G4.Models;
using G4.Services.Domain.V4.Repositories;
using G4.Settings;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Mcp
{
    /// <summary>
    /// Exposes flow management to MCP clients through the repository shared with the REST flows controller.
    /// </summary>
    /// <remarks>
    /// Flows are stored automations addressed by namespace and key; an omitted namespace selects
    /// <see cref="FlowsRepository.DefaultNamespace"/>. Unknown flows, invalid manifests, and identity conflicts become
    /// MCP tool errors through <see cref="McpResults"/>. Clearing every flow is intentionally available only through
    /// REST, so an agent cannot wipe the collection with one call. Optional parameters carry default values because
    /// the MCP schema marks a parameter optional only when it has one.
    /// </remarks>
    /// <param name="flows">The singleton flows repository; its lifetime belongs to the application host.</param>
    [McpServerToolType]
    public sealed class FlowTools(IFlowsRepository flows)
    {
        #region *** Constants    ***

        // Shared description of the flow key argument used by every single-flow tool.
        private const string KeyDescription = "The flow key. Flow keys are unique within a namespace.";

        // Shared description of the optional namespace argument used by every single-flow tool.
        private const string NamespaceDescription = "The flow namespace. Defaults to " +
            FlowsRepository.DefaultNamespace + " when omitted.";

        #endregion

        #region *** Fields       ***

        // Serializes caller values with the application JSON contract while omitting absent values, so a REST-shaped
        // body built from optional tool arguments never overrides the flow's own values with null.
        private static readonly JsonSerializerOptions s_bodyOptions = new(AppSettings.JsonOptions)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        #endregion

        #region *** Methods      ***

        /// <summary>
        /// Returns the stored manifest of one flow.
        /// </summary>
        /// <param name="key">The flow key.</param>
        /// <param name="namespace">The optional flow namespace.</param>
        /// <returns>The manifest as JSON text and structured content, or a tool error for an unknown flow.</returns>
        [McpServerTool(Name = "g4.GetFlow", Title = "Get G4 Flow", ReadOnly = true, Idempotent = true)]
        [Description("Gets the stored manifest of one flow, including its parameters and its Base64 automation.")]
        public async Task<CallToolResult> GetFlowAsync(
            [Description(KeyDescription)] string key,
            [Description(NamespaceDescription)] string @namespace = null)
        {
            // Read through the shared repository so an unknown flow fails exactly as it does over REST.
            return await McpResults
                .InvokeAsync(operation: () => Task.FromResult<object>(result: flows.GetFlow(@namespace, key)))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Lists the stored flows of every namespace, or of one namespace.
        /// </summary>
        /// <param name="namespace">The optional namespace filter.</param>
        /// <returns>The flow count and manifests as JSON text and structured content.</returns>
        [McpServerTool(Name = "g4.GetFlows", Title = "Get G4 Flows", ReadOnly = true, Idempotent = true)]
        [Description("Lists the stored flows with their manifests. Filter by namespace to narrow the result.")]
        public async Task<CallToolResult> GetFlowsAsync(
            [Description("The namespace to filter by. When omitted, flows of every namespace are returned.")]
            string @namespace = null)
        {
            // Wrap the manifests with their count, mirroring the REST X-Manifest-Count header in structured content.
            return await McpResults
                .InvokeAsync(operation: () =>
                {
                    var manifests = flows.GetFlows(@namespace);
                    return Task.FromResult<object>(result: new { Count = manifests.Length, Flows = manifests });
                })
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Runs one flow with optional parameter values and caller overrides, and waits for it to finish.
        /// </summary>
        /// <param name="key">The flow key or alias.</param>
        /// <param name="namespace">The optional flow namespace.</param>
        /// <param name="parameters">The optional parameter values by name.</param>
        /// <param name="argument">The optional parameter values in command-line form.</param>
        /// <param name="referenceId">The optional automation identifier used to stop the run.</param>
        /// <param name="authentication">The optional caller authentication.</param>
        /// <param name="driverParameters">The optional driver parameters that fill what the flow leaves out.</param>
        /// <param name="settings">The optional automation settings that fill what the flow leaves out.</param>
        /// <returns>
        /// The automation responses by group identifier, or a tool error for an unknown or invalid flow.
        /// </returns>
        [McpServerTool(Name = "g4.InvokeFlow", Title = "Invoke G4 Flow", Destructive = false, Idempotent = false)]
        [Description("Runs a stored flow and waits for it to finish. Parameter values replace the " +
            "{{$ parameters.Name }} placeholders anywhere in the flow; send them as parameters, as an argument in " +
            "command-line form ({{$ --Name:value}}), or both, and on a name in both the argument value is used. " +
            "A declared parameter without a value receives its default. The caller's authentication token always " +
            "replaces the flow's token; the other authentication, driverParameters, and settings values only fill " +
            "what the flow leaves out. Set referenceId to a unique value to stop the run with g4.StopFlowInvocation. " +
            "Returns the automation responses by group ID.")]
        public async Task<CallToolResult> InvokeFlowAsync(
            [Description("The flow key or alias.")] string key,
            [Description(NamespaceDescription)] string @namespace = null,
            [Description("Parameter values by name, for example { \"UserName\": \"admin\" }.")]
            Dictionary<string, object> parameters = null,
            [Description("Parameter values in command-line form, for example {{$ --Retries:3}}.")]
            string argument = null,
            [Description("A unique automation ID for this run, used by g4.StopFlowInvocation.")]
            string referenceId = null,
            [Description("The caller's G4 authentication, for example { \"token\": \"...\" }.")]
            Dictionary<string, object> authentication = null,
            [Description("Driver parameters, such as driver and driverBinaries, that fill what the flow leaves out.")]
            Dictionary<string, object> driverParameters = null,
            [Description("Automation settings that fill what the flow leaves out.")]
            Dictionary<string, object> settings = null)
        {
            // Build the REST invoke body from the supplied values only, so absent overrides keep the flow's own values.
            var arguments = new { argument, authentication, driverParameters, parameters, referenceId, settings };
            var invokeModel = ConvertToInvokeModel(arguments);

            // Run through the shared repository; the call blocks until the flow finishes, as REST invoke does.
            return await McpResults
                .InvokeAsync(operation: () =>
                {
                    var responses = flows.InvokeFlow(@namespace, key, invokeModel);
                    return Task.FromResult<object>(result: responses);
                })
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a flow from a native automation, or overwrites the flow with the same namespace and key.
        /// </summary>
        /// <param name="key">The flow key.</param>
        /// <param name="automation">The native G4 automation, which the tool encodes as Base64.</param>
        /// <param name="namespace">The optional flow namespace.</param>
        /// <param name="aliases">The optional alternative keys.</param>
        /// <param name="summary">The optional one-line summary.</param>
        /// <param name="description">The optional longer description.</param>
        /// <param name="categories">The optional discovery categories.</param>
        /// <param name="parameters">The optional parameters callers supply when they run the flow.</param>
        /// <returns>The stored identity, or a tool error for an invalid manifest or an identity conflict.</returns>
        [McpServerTool(Name = "g4.RegisterFlow", Title = "Register G4 Flow", Destructive = false, Idempotent = true)]
        [Description("Registers a native G4 automation as a stored flow, or overwrites the flow with the same " +
            "namespace and key. Pass the automation as a JSON object, for example the result of g4.GetAutomation; " +
            "the tool encodes it as Base64. Declare parameters to expose {{$ parameters.Name }} placeholders to " +
            "g4.InvokeFlow callers.")]
        public async Task<CallToolResult> RegisterFlowAsync(
            [Description(KeyDescription)] string key,
            [Description("The native G4 automation JSON object to store, for example the result of g4.GetAutomation.")]
            Dictionary<string, object> automation,
            [Description(NamespaceDescription)] string @namespace = null,
            [Description("Alternative keys that also identify the flow.")] string[] aliases = null,
            [Description("A one-line summary shown when the flow is discovered.")] string summary = null,
            [Description("A longer description of what the flow does.")] string description = null,
            [Description("Categories used to group the flow.")] string[] categories = null,
            [Description("Parameters callers can supply to g4.InvokeFlow.")] FlowParameterModel[] parameters = null)
        {
            // Build the REST manifest; only supplied descriptive values replace the manifest defaults.
            var manifest = new G4FlowManifestModel
            {
                Aliases = aliases ?? [],
                Automation = automation is null ? null : NewAutomationBase64(automation),
                Categories = categories ?? [],
                Description = string.IsNullOrEmpty(description) ? [] : [description],
                Key = key,
                Namespace = @namespace,
                Parameters = parameters?.Select(ConvertToParameter).ToArray() ?? [],
                Summary = string.IsNullOrEmpty(summary) ? [] : [summary]
            };

            // Store through the shared repository, then report the identity the flows client normalized.
            return await McpResults
                .InvokeAsync(operation: () =>
                {
                    flows.AddFlow(manifest);
                    return Task.FromResult<object>(result: new { Namespace = NewNamespace(@namespace), Key = key });
                })
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Removes one stored flow.
        /// </summary>
        /// <param name="key">The flow key.</param>
        /// <param name="namespace">The optional flow namespace.</param>
        /// <returns>The removed identity, or a tool error for an unknown flow.</returns>
        [McpServerTool(Name = "g4.RemoveFlow", Title = "Remove G4 Flow", Destructive = true, Idempotent = true)]
        [Description("Removes one stored flow from the Flows collection and the flows cache.")]
        public async Task<CallToolResult> RemoveFlowAsync(
            [Description(KeyDescription)] string key,
            [Description(NamespaceDescription)] string @namespace = null)
        {
            // Remove through the shared repository so an unknown flow fails exactly as it does over REST.
            return await McpResults
                .InvokeAsync(operation: () =>
                {
                    flows.RemoveFlow(@namespace, key);
                    var removed = new { Namespace = NewNamespace(@namespace), Key = key, Removed = true };
                    return Task.FromResult<object>(result: removed);
                })
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Stops a running flow invocation.
        /// </summary>
        /// <param name="automationId">The automation identifier of the running invocation.</param>
        /// <returns>The automation identifier, the stop status, and the exit code of the flows client.</returns>
        [McpServerTool(
            Name = "g4.StopFlowInvocation", Title = "Stop G4 Flow Invocation", Destructive = true, Idempotent = true)]
        [Description("Stops a running flow invocation by its automation ID (the referenceId sent to g4.InvokeFlow, " +
            "or the flow's own reference ID). Its driver sessions are closed, so the flow stops at its next step. " +
            "Returns status stopped when the stop request completed, or failed with exitCode 1 on an error.")]
        public async Task<CallToolResult> StopFlowInvocationAsync(
            [Description("The automation ID of the running flow invocation to stop.")] string automationId)
        {
            // Stop through the shared repository; the status names the outcome because the JSON contract omits a zero
            // exit code, which would otherwise leave a successful stop without any outcome field.
            return await McpResults
                .InvokeAsync(operation: () =>
                {
                    var exitCode = flows.StopInvocation(automationId);
                    var status = exitCode == 0 ? "stopped" : "failed";
                    var stopped = new { AutomationId = automationId, Status = status, ExitCode = exitCode };
                    return Task.FromResult<object>(result: stopped);
                })
                .ConfigureAwait(false);
        }

        // Converts the supplied invoke values into the REST invoke model through the application JSON contract, so
        // open objects such as authentication and settings bind to their typed members exactly as a REST body does.
        private static G4FlowInvokeModel ConvertToInvokeModel(object arguments)
        {
            // Serialize the anonymous values once and bind them; null members leave the model's defaults in place.
            var node = JsonSerializer.SerializeToNode(arguments, options: s_bodyOptions);
            return node.Deserialize<G4FlowInvokeModel>(options: AppSettings.JsonOptions);
        }

        // Converts one MCP flow parameter into the plugin parameter shape stored in the manifest.
        private static PluginParameterModel ConvertToParameter(FlowParameterModel parameter)
        {
            // Map only supplied values so omitted members keep the plugin parameter defaults.
            return new PluginParameterModel
            {
                Default = parameter.Default,
                Description = string.IsNullOrEmpty(parameter.Description) ? [] : [parameter.Description],
                Mandatory = parameter.Mandatory ?? false,
                Name = parameter.Name
            };
        }

        // Encodes a native automation exactly as REST callers do: the automation JSON, UTF-8 encoded as Base64.
        private static string NewAutomationBase64(Dictionary<string, object> automation)
        {
            // Serialize with the application contract so the stored automation matches a REST-supplied flow.
            var json = JsonSerializer.Serialize(automation, options: s_bodyOptions);
            return json.ConvertToBase64();
        }

        // Resolves the namespace the flows client assigns, so results name the identity that was actually stored.
        private static string NewNamespace(string @namespace)
        {
            // Apply the flows client's normalization: an omitted or empty namespace selects the default.
            return string.IsNullOrEmpty(@namespace) ? FlowsRepository.DefaultNamespace : @namespace;
        }

        #endregion
    }
}
