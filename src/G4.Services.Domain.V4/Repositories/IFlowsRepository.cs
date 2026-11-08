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
    /// An omitted or empty namespace selects G4.System, as the flows client normalizes it. An unknown flow is a normal
    /// outcome: the Try methods return <see langword="false"/> and the transports answer with a not-found result.
    /// Real failures use ordinary exception types that both transports map to caller-visible errors:
    /// <see cref="ArgumentException"/> for an invalid manifest, flow, or data source, and
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
        /// Stops a running flow invocation by cancelling it and closing its driver sessions.
        /// </summary>
        /// <param name="automationId">The automation identifier of the running invocation.</param>
        /// <returns>0 when the stop request completed; 1 when an error occurred.</returns>
        int StopInvocation(string automationId);

        /// <summary>
        /// Gets the stored manifest of one flow, when it exists.
        /// </summary>
        /// <param name="namespace">The flow namespace; null or empty selects G4.System.</param>
        /// <param name="key">The flow key.</param>
        /// <param name="manifest">The stored flow manifest, or <see langword="null"/> when no flow exists.</param>
        /// <returns><see langword="true"/> when the flow exists; otherwise, <see langword="false"/>.</returns>
        bool TryGetFlow(string @namespace, string key, out G4FlowManifestModel manifest);

        /// <summary>
        /// Runs one flow with the caller's parameter values and overrides, when it exists, and waits for it to finish.
        /// </summary>
        /// <param name="namespace">The flow namespace; null or empty selects G4.System.</param>
        /// <param name="key">The flow key or alias.</param>
        /// <param name="invokeModel">The parameter values, authentication, driver parameters, and settings.</param>
        /// <param name="responses">
        /// The automation responses keyed by group identifier, or <see langword="null"/> when no flow exists.
        /// </param>
        /// <returns><see langword="true"/> when the flow exists and ran; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentException">
        /// The flow cannot be decoded, is not valid JSON after its parameters were applied, or its data source fails.
        /// </exception>
        bool TryInvokeFlow(
            string @namespace,
            string key,
            G4FlowInvokeModel invokeModel,
            out IDictionary<string, G4AutomationResponseModel> responses);

        /// <summary>
        /// Removes one flow from the Flows collection and the flows cache, when it exists.
        /// </summary>
        /// <param name="namespace">The flow namespace; null or empty selects G4.System.</param>
        /// <param name="key">The flow key.</param>
        /// <returns>
        /// <see langword="true"/> when the flow existed and was removed; otherwise, <see langword="false"/>.
        /// </returns>
        bool TryRemoveFlow(string @namespace, string key);

        #endregion
    }
}
