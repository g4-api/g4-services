using G4.Api.Abstractions;
using G4.Api.Models;
using G4.Exceptions;
using G4.Models;
using G4.Models.Events;
using G4.Plugins;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace G4.Services.UnitTests.Mcp
{
    // Keeps flows in memory and reproduces the status codes and exceptions of the G4 flows client, so repository
    // and MCP tests exercise every failure branch without the shared LiteDB collection or a real engine run.
    // Reserved keys select the failure the real client raises: a conflict, an invalid flow, or a data source error.
    internal sealed class FakeFlowsClient : IFlowsClient
    {
        #region *** Constants    ***

        // Names the flow whose registration conflicts with another cached flow, raised wrapped as the engine does.
        internal const string ConflictKey = "ConflictingFlow";

        // Names the flow whose data source cannot be read at invocation time.
        internal const string InvalidDataSourceKey = "InvalidDataSourceFlow";

        // Names the flow whose stored automation cannot be built into a valid automation at invocation time.
        internal const string InvalidFlowKey = "InvalidFlow";

        // Namespace the real client assigns to a flow addressed without one.
        private const string DefaultNamespace = "G4.System";

        #endregion

        #region *** Events     ***

        // The fake raises no engine events; empty accessors satisfy the interface without unused-event warnings.

        /// <inheritdoc />
        public event EventHandler<AutomationCallbackEventArgs> AutomationCallback { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<AutomationEventArgs> AutomationInvoked { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<AutomationEventArgs> AutomationInvoking { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<AutomationQueueModel> AutomationRequestInitialized { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<AutomationEventArgs> AutomationStatusChanged { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<int> AutomationStopped { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<JobEventArgs> JobInvoked { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<JobEventArgs> JobInvoking { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<JobEventArgs> JobStatusChanged { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<LogEventArgs> LogCreated { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<LogEventArgs> LogCreating { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<(RuleEventArgs EventArguments, Exception Exception)> OnRuleError
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public event EventHandler<(PluginBase Plugin, G4RuleModelBase Rule)> PluginCreated { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<RuleCallbackEventArgs> RuleCallback { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<RuleEventArgs> RuleInvoked { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<RuleEventArgs> RuleInvoking { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<RuleEventArgs> RuleStatusChanged { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<StageEventArgs> StageInvoked { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<StageEventArgs> StageInvoking { add { } remove { } }

        /// <inheritdoc />
        public event EventHandler<StageEventArgs> StageStatusChanged { add { } remove { } }

        #endregion

        #region *** Properties   ***

        // Retains every invoke model so tests can assert how tool arguments were bound.
        internal ConcurrentQueue<G4FlowInvokeModel> Invocations { get; } = [];

        /// <inheritdoc />
        public ILogger Logger => null;

        // Holds the stored manifests by normalized namespace-qualified key.
        internal ConcurrentDictionary<string, G4FlowManifestModel> Manifests { get; } =
            new(comparer: StringComparer.OrdinalIgnoreCase);

        // Retains every automation identifier passed to stop.
        internal ConcurrentQueue<string> Stopped { get; } = [];

        #endregion

        #region *** Methods      ***

        /// <inheritdoc />
        public int AddFlow(G4FlowManifestModel manifest)
        {
            // Reject a missing key or an automation that is not Base64 JSON, as the real client does before writing.
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Key) || !TestAutomation(manifest.Automation))
            {
                return 400;
            }

            // Raise the cache identity conflict wrapped, as the engine surfaces it through reflection-based calls.
            if (manifest.Key == ConflictKey)
            {
                var conflict = new InvalidOperationException(message: $"The flow key '{ConflictKey}' is already used.");
                throw new TargetInvocationException(conflict);
            }

            // Store or overwrite the flow under its normalized identity.
            Manifests[NewIdentity(manifest.Namespace, manifest.Key)] = manifest;
            return 204;
        }

        /// <inheritdoc />
        public void ClearFlows()
        {
            // Remove every stored flow.
            Manifests.Clear();
        }

        /// <inheritdoc />
        public (int StatusCode, G4FlowManifestModel Manifest) GetFlow(string @namespace, string key)
        {
            // Return the stored manifest, or the not-found status for an unknown identity.
            var isFound = Manifests.TryGetValue(key: NewIdentity(@namespace, key), value: out var manifest);
            return isFound ? (200, manifest) : (404, null);
        }

        /// <inheritdoc />
        public IEnumerable<G4FlowManifestModel> GetFlows() => Manifests.Values;

        /// <inheritdoc />
        public IEnumerable<G4FlowManifestModel> GetFlows(string @namespace)
        {
            // Filter by normalized namespace so an omitted namespace matches G4.System.
            var flowNamespace = string.IsNullOrEmpty(@namespace) ? DefaultNamespace : @namespace;
            return Manifests.Values.Where(manifest => NewNamespace(manifest.Namespace) == flowNamespace);
        }

        /// <inheritdoc />
        public (int StatusCode, IDictionary<string, G4AutomationResponseModel> Responses) Invoke(
            string @namespace,
            string key)
        {
            // Delegate to the complete overload with an empty invoke model.
            return Invoke(@namespace, key, invokeModel: new G4FlowInvokeModel());
        }

        /// <inheritdoc />
        public (int StatusCode, IDictionary<string, G4AutomationResponseModel> Responses) Invoke(
            string @namespace,
            string key,
            G4FlowInvokeModel invokeModel)
        {
            // Record the bound model, then reproduce the reserved failures before the not-found check.
            Invocations.Enqueue(item: invokeModel);
            if (key == InvalidDataSourceKey)
            {
                throw new G4DataSourceException(message: "The data source 'fixture.csv' was not found.");
            }
            if (key == InvalidFlowKey)
            {
                return (400, null);
            }
            if (!Manifests.ContainsKey(NewIdentity(@namespace, key)))
            {
                return (404, null);
            }

            // Return one empty response group, which is enough to assert the result shape.
            var responses = new Dictionary<string, G4AutomationResponseModel> { ["fixture-group"] = new() };
            return (200, responses);
        }

        /// <inheritdoc />
        public int RemoveFlow(string @namespace, string key)
        {
            // Remove the stored flow, or report the not-found status for an unknown identity.
            return Manifests.TryRemove(key: NewIdentity(@namespace, key), value: out _) ? 204 : 404;
        }

        /// <inheritdoc />
        public int StopAutomation(string automationId)
        {
            // Record the stop request and report success, as the real client does when no error occurs.
            Stopped.Enqueue(item: automationId);
            return 0;
        }

        // Builds the case-insensitive identity under which a flow is stored.
        private static string NewIdentity(string @namespace, string key) => $"{NewNamespace(@namespace)}.{key}";

        // Applies the real client's namespace normalization.
        private static string NewNamespace(string @namespace)
        {
            // Preserve explicit namespaces while assigning omitted flows to the default namespace.
            return string.IsNullOrEmpty(@namespace) ? DefaultNamespace : @namespace;
        }

        // Tests whether an automation is Base64 text that decodes into a JSON object, as the real client requires.
        private static bool TestAutomation(string automation)
        {
            try
            {
                // Decode and parse; any failure marks the automation invalid.
                var json = Encoding.UTF8.GetString(bytes: Convert.FromBase64String(s: automation ?? string.Empty));
                using var document = JsonDocument.Parse(json);
                return document.RootElement.ValueKind == JsonValueKind.Object;
            }
            catch (Exception exception) when (exception is FormatException or JsonException)
            {
                return false;
            }
        }

        #endregion
    }
}
