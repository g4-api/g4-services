using System;
using System.Collections.Generic;

namespace G4.Models
{
    /// <summary>
    /// Represents the error reply that the automation notifications hub sends to its caller when an automation fails
    /// or is stopped.
    /// </summary>
    /// <remarks>
    /// SignalR clients, such as the embedded designer, receive this model as the argument of the <c>StartAutomation</c>
    /// message. It does not derive from <c>ProblemDetails</c> on purpose: that HTTP error body carries a
    /// <c>[JsonExtensionData]</c> dictionary, which the Hub's <c>Dictionary&lt;string, object&gt;</c> converter writes as
    /// an object without a property name. The frame is then invalid JSON, and the client drops the connection before it
    /// can read the error. The members keep the JSON names the clients already read: <c>errors</c>, <c>request</c>, and
    /// <c>traceId</c>.
    /// </remarks>
    public class AutomationErrorModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the error messages, keyed by the failing area, for example <c>Automation</c>.
        /// </summary>
        public Dictionary<string, string[]> Errors { get; set; } = [];

        /// <summary>
        /// Gets or sets the automation request that failed.
        /// </summary>
        /// <remarks>
        /// Null when the reply reports a stop, because a stop does not reject the request; the serializer then omits
        /// the member from the frame.
        /// </remarks>
        public G4AutomationModel Request { get; set; }

        /// <summary>
        /// Gets or initializes the time-based identifier of the reply, in the format the Hub's HTTP error bodies use
        /// outside a request.
        /// </summary>
        public string TraceId { get; init; } = DateTime.Now.ToString("yyyyMMdd-HHmmss-fffffff");
        #endregion
    }
}
