using G4.Api.Abstractions;
using G4.Exceptions;
using G4.Models;

using System;
using System.Collections.Generic;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Translates the status codes of the G4 flows client into the exception contract of
    /// <see cref="IFlowsRepository"/>.
    /// </summary>
    /// <remarks>
    /// The flows client owns persistence and the flows cache; this repository adds no state. Exceptions are raised as
    /// root exceptions without inner exceptions, because the REST error middleware maps the base exception type.
    /// </remarks>
    /// <param name="flows">The flows client of the application-scoped G4 client.</param>
    public sealed class FlowsRepository(IFlowsClient flows) : IFlowsRepository
    {
        #region *** Constants    ***

        /// <summary>
        /// Specifies the namespace the flows client assigns to a flow addressed without one.
        /// </summary>
        public const string DefaultNamespace = "G4.System";

        // Success status the flows client returns for add and remove.
        private const int NoContentStatus = 204;

        // Status the flows client returns for an unknown flow identity.
        private const int NotFoundStatus = 404;

        // Success status the flows client returns for get and invoke.
        private const int OkStatus = 200;

        #endregion

        #region *** Methods      ***

        /// <inheritdoc />
        public void AddFlow(G4FlowManifestModel manifest)
        {
            // Persist and cache the flow; the client validates the key and the automation before any write.
            var statusCode = InvokeConflictAware(operation: () => flows.AddFlow(manifest));
            if (statusCode == NoContentStatus)
            {
                return;
            }

            // Describe the rejected manifest, because the client reports validation failures only as a status code.
            var message = "The flow manifest must have a key and an automation encoded as Base64 that deserializes " +
                "into a G4AutomationModel.";
            throw new ArgumentException(message, paramName: nameof(manifest));
        }

        /// <inheritdoc />
        public void ClearFlows()
        {
            // Remove every stored flow; the client clears the flows cache after persistence succeeds.
            flows.ClearFlows();
        }

        /// <inheritdoc />
        public G4FlowManifestModel GetFlow(string @namespace, string key)
        {
            // Read the stored manifest and report an unknown identity with the namespace the client searched.
            var (statusCode, manifest) = flows.GetFlow(@namespace, key);
            if (statusCode == OkStatus)
            {
                return manifest;
            }

            throw NewFlowNotFoundException(@namespace, key);
        }

        /// <inheritdoc />
        public G4FlowManifestModel[] GetFlows()
        {
            // Materialize every manifest so callers can count and serialize the result without re-enumerating.
            return [.. flows.GetFlows()];
        }

        /// <inheritdoc />
        public G4FlowManifestModel[] GetFlows(string @namespace)
        {
            // Treat an omitted namespace as every namespace, matching the REST list contract.
            return string.IsNullOrEmpty(@namespace) ? GetFlows() : [.. flows.GetFlows(@namespace)];
        }

        /// <inheritdoc />
        public IDictionary<string, G4AutomationResponseModel> InvokeFlow(
            string @namespace,
            string key,
            G4FlowInvokeModel invokeModel)
        {
            try
            {
                // Run the flow and wait for it; an omitted invoke model runs the flow as stored.
                var (statusCode, responses) = flows.Invoke(@namespace, key, invokeModel ?? new G4FlowInvokeModel());

                // Map the client's status codes: success, unknown identity, or a flow that could not be built.
                if (statusCode == OkStatus)
                {
                    return responses;
                }
                if (statusCode == NotFoundStatus)
                {
                    throw NewFlowNotFoundException(@namespace, key);
                }

                var message = "The flow could not be decoded into an automation, or it is not valid JSON after its " +
                    "parameters were applied. Parameter values are inserted as they are: check values that hold " +
                    "quotes or backslashes.";
                throw new ArgumentException(message, paramName: nameof(invokeModel));
            }
            catch (G4DataSourceException exception)
            {
                // Report an unreadable data source as invalid input; the message explains which source failed.
                var message = $"The flow data source cannot be read: {exception.Message}";
                throw new ArgumentException(message, paramName: nameof(invokeModel));
            }
        }

        /// <inheritdoc />
        public void RemoveFlow(string @namespace, string key)
        {
            // Remove the flow and report an unknown identity with the namespace the client searched.
            var statusCode = flows.RemoveFlow(@namespace, key);
            if (statusCode == NoContentStatus)
            {
                return;
            }

            throw NewFlowNotFoundException(@namespace, key);
        }

        /// <inheritdoc />
        public int StopInvocation(string automationId)
        {
            // Cancel the invocation and close its sessions; the client reports the outcome as an exit code.
            return flows.StopAutomation(automationId);
        }

        // Runs one flows-client operation and unwraps a cache identity conflict into its root exception, so both
        // transports observe the InvalidOperationException that the client raises before persistence changes.
        private static int InvokeConflictAware(Func<int> operation)
        {
            try
            {
                // Run the operation; a conflict is raised before any write, so nothing was stored on failure.
                return operation();
            }
            catch (Exception exception) when (exception is not InvalidOperationException
                && exception.GetBaseException() is InvalidOperationException conflict)
            {
                // Rethrow the root conflict without its wrappers so MCP and REST map it to the same error.
                throw new InvalidOperationException(conflict.Message);
            }
        }

        // Creates the unknown-flow failure, naming the namespace the flows client actually searched.
        private static KeyNotFoundException NewFlowNotFoundException(string @namespace, string key)
        {
            // Resolve the namespace exactly as the flows client normalizes it.
            var flowNamespace = string.IsNullOrEmpty(@namespace) ? DefaultNamespace : @namespace;
            var message = $"The flow '{flowNamespace}.{key}' was not found. Please try re-adding the flow or reload " +
                "the service.";

            return new KeyNotFoundException(message);
        }

        #endregion
    }
}
