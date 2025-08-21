using System.Collections.Generic;

namespace G4.Models
{
    /// <summary>
    /// Response model for the Copilot "list tools" JSON-RPC method.
    /// Encapsulates the request ID, the protocol version, and the result payload.
    /// </summary>
    public class ToolsResponseModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the error information if the request failed.
        /// This property is optional and will be null if the request was successful.
        /// </summary>
        public ToolsErrorModel Error { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the JSON-RPC request.
        /// This value is echoed back in the response to correlate it with the original request.
        /// </summary>
        public object Id { get; set; }

        /// <summary>
        /// Gets or sets the JSON-RPC protocol version.
        /// Defaults to "2.0" in compliance with the specification.
        /// </summary>
        public string Jsonrpc { get; set; } = "2.0";

        /// <summary>
        /// Gets or sets the result wrapper containing the list of available tools.
        /// </summary>
        public object Result { get; set; } = new();
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Wraps the payload returned by the "list tools" method.
        /// Contains the collection of <see cref="McpToolModel"/> instances.
        /// </summary>
        public class ResultModel
        {
            /// <summary>
            /// Gets or sets the list of tools that the Copilot agent can invoke.
            /// </summary>
            public IEnumerable<McpToolModel> Tools { get; set; } = [];
        }
        #endregion
    }
}
