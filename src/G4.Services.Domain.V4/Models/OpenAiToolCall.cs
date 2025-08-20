namespace G4.Models
{
    /// <summary>
    /// Captures information about a tool invocation requested by the model.
    /// Includes the function details, invocation identifier, and position in the response sequence.
    /// </summary>
    public class OpenAiToolCall
    {
        /// <summary>
        /// The function call details for this tool invocation.
        /// Contains the function name and JSON-encoded arguments.
        /// </summary>
        public OpenAiFunctionCall Function { get; set; }

        /// <summary>
        /// A unique identifier for this tool call.
        /// Useful for correlating streaming deltas to the same invocation.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The zero-based position of this tool call among multiple calls.
        /// Null if positional ordering is not provided.
        /// </summary>
        public int? Index { get; set; }

        /// <summary>
        /// The type of the tool call (e.g., "function", "tool").
        /// Mapped from the JSON "type" property.
        /// </summary>
        public string Type { get; set; }
    }
}
