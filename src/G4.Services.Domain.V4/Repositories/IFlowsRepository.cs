using G4.Models;

using System;
using System.Collections.Generic;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Defines the flow lifecycle shared by the REST flows controller and the MCP flow tools.
    /// </summary>
    /// <remarks>
    /// Flows are complete automations, stored as Base64 in the Flows collection and cached by namespace and key.
    /// An omitted or empty namespace selects G4.System, as the flows client normalizes it. Failures use ordinary
    /// exception types that both transports map to caller-visible errors: <see cref="ArgumentException"/> for an
    /// invalid manifest, flow, or data source, <see cref="KeyNotFoundException"/> for an unknown flow, and
    /// <see cref="InvalidOperationException"/> when a key or alias conflicts with another cached flow.
    /// </remarks>
    public interface IFlowsRepository
    {
        #region *** Methods      ***

        /// <summary>
        /// Adds a flow, or overwrites the stored flow with the same namespace and key, and updates the flows cache.
        /// </summary>
        /// <param name="manifest">
        /// The flow manifest whose automation is the Base64 encoding of an automation JSON.
        /// </param>
        /// <exception cref="ArgumentException">
        /// The manifest has no key, or its automation is missing, is not valid Base64, or does not deserialize.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The key or an alias conflicts with another cached flow.
        /// </exception>
        void AddFlow(G4FlowManifestModel manifest);

        /// <summary>
        /// Removes every flow from the Flows collection and the flows cache.
        /// </summary>
        void ClearFlows();

        /// <summary>
        /// Gets the stored manifest of one flow.
        /// </summary>
        /// <param name="namespace">The flow namespace; null or empty selects G4.System.</param>
        /// <param name="key">The flow key.</param>
        /// <returns>The stored flow manifest.</returns>
        /// <exception cref="KeyNotFoundException">No flow exists with the namespace and key.</exception>
        G4FlowManifestModel GetFlow(string @namespace, string key);

        /// <summary>
        /// Gets the manifests of every stored flow.
        /// </summary>
        /// <returns>The stored flow manifests of every namespace.</returns>
        G4FlowManifestModel[] GetFlows();

        /// <summary>
        /// Gets the manifests of the stored flows in one namespace.
        /// </summary>
        /// <param name="namespace">The namespace to filter by; null or empty returns every namespace.</param>
        /// <returns>The stored flow manifests of the namespace.</returns>
        G4FlowManifestModel[] GetFlows(string @namespace);

        /// <summary>
        /// Runs one flow with the caller's parameter values and overrides, and waits for it to finish.
        /// </summary>
        /// <param name="namespace">The flow namespace; null or empty selects G4.System.</param>
        /// <param name="key">The flow key or alias.</param>
        /// <param name="invokeModel">The parameter values, authentication, driver parameters, and settings.</param>
        /// <returns>The automation responses keyed by group identifier.</returns>
        /// <exception cref="KeyNotFoundException">No flow exists with the namespace and key or alias.</exception>
        /// <exception cref="ArgumentException">
        /// The flow cannot be decoded, is not valid JSON after its parameters were applied, or its data source fails.
        /// </exception>
        IDictionary<string, G4AutomationResponseModel> InvokeFlow(
            string @namespace,
            string key,
            G4FlowInvokeModel invokeModel);

        /// <summary>
        /// Removes one flow from the Flows collection and the flows cache.
        /// </summary>
        /// <param name="namespace">The flow namespace; null or empty selects G4.System.</param>
        /// <param name="key">The flow key.</param>
        /// <exception cref="KeyNotFoundException">No flow exists with the namespace and key.</exception>
        void RemoveFlow(string @namespace, string key);

        /// <summary>
        /// Stops a running flow invocation by cancelling it and closing its driver sessions.
        /// </summary>
        /// <param name="automationId">The automation identifier of the running invocation.</param>
        /// <returns>0 when the stop request completed; 1 when an error occurred.</returns>
        int StopInvocation(string automationId);

        #endregion
    }
}
