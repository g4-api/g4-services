using G4.Models;

namespace G4.Services.Domain.V4.Models
{
    /// <summary>
    /// Represents one rule execution entry retained in a session buffer and exposed through MCP responses.
    /// </summary>
    /// <remarks>
    /// The property-backed shape preserves the timestamp and rule during System.Text.Json serialization.
    /// The entry references the executed rule and does not clone or mutate it.
    /// </remarks>
    public sealed class BufferResponseModel
    {
        /// <summary>
        /// Gets or sets the executed rule retained by the session buffer.
        /// </summary>
        public G4RuleModelBase Rule { get; set; }

        /// <summary>
        /// Gets or sets the Unix timestamp, in milliseconds, recorded when the rule was executed.
        /// </summary>
        public long Timestamp { get; set; }
    }
}
