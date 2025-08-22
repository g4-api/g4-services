using Swashbuckle.AspNetCore.Annotations;

namespace G4.Models.Schema
{
    /// <summary>
    /// Input schema for the <c>FindTool</c> operation.
    /// Provides the data needed to locate a tool within the MCP tool registry.
    /// </summary>
    [SwaggerSchema(description: "Schema for locating a tool by name or intent within the MCP tool registry.")]
    public class FindToolInputSchema
    {
        /// <summary>
        /// The unique chat identifier associated with this request.
        /// Used to correlate tool lookups with a specific chat session.
        /// </summary>
        [SwaggerSchema(description: "The unique chat identifier used to correlate the request with a specific session.")]
        public object Id { get; set; }

        /// <summary>
        /// The functional intent that describes the purpose of the tool being requested.
        /// If the <see cref="ToolName"/> is missing or not an exact match, this intent can be
        /// leveraged for vector-based similarity search to find the most relevant tool.
        /// </summary>
        [SwaggerSchema(description: "The intent or purpose for which the tool is requested. " +
            "Used for vector-based lookup if the tool name is not provided or inaccurate.")]
        public string Intent { get; set; }

        /// <summary>
        /// The name of the tool to locate within the MCP tool collection.
        /// If not provided or inaccurate, the <see cref="Intent"/> can be used as a fallback
        /// to approximate the best matching tool.
        /// </summary>
        [SwaggerSchema(description: "The exact or partial name of the tool to locate. " +
            "If missing or inaccurate, the intent will be used instead.")]
        public string ToolName { get; set; }
    }
}
