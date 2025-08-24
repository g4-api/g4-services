using Swashbuckle.AspNetCore.Annotations;

namespace G4.Models.Schema
{
    /// <summary>
    /// Input schema for retrieving a collection of available tools.
    /// Allows optional filtering by intent (purpose/capability) and/or tool types,
    /// and associates the request with a chat/session identifier.
    /// </summary>
    [SwaggerSchema(description: "Schema for listing available tools, optionally filtered by " +
        "intent (purpose) and/or type(s).")]
    public class GetToolsInputSchema
    {
        /// <summary>
        /// The unique chat identifier associated with this request.
        /// Used to correlate tool lookups with a specific chat session.
        /// </summary>
        [SwaggerSchema(description: "The unique chat identifier used to correlate the request " +
            "with a specific session.")]
        public object Id { get; set; }

        /// <summary>
        /// Optional intent describing the purpose or capability you are looking for
        /// (e.g., “search”, “navigate”, “screenshot”). When provided, the tool list
        /// may be filtered and/or ranked by semantic or vector similarity to this intent.
        /// If null or empty, no intent-based filtering is applied.
        /// </summary>
        [SwaggerSchema(description: "Optional: purpose/capability to filter or rank tools by " +
            "(semantic/vector similarity may be used). If omitted, no intent filter is applied.")]
        public string Intent { get; set; }

        /// <summary>
        /// Optional list of tool types to include. If omitted, all tool types are included.
        /// </summary>
        [SwaggerSchema(description: "Optional: list of tool types to include. If omitted, tools " +
            "of all types are returned.")]
        public string[] Types { get; set; }
    }
}
