using G4.Models;

namespace G4.Services.Domain.V4.Models
{
    /// <summary>
    /// Represents the MCP response payload that carries the session buffer entries
    /// retained from executed rules.
    /// </summary>
    /// <remarks>
    /// The property-backed shape preserves the timestamp and rule references during
    /// System.Text.Json serialization. Each entry references the executed rule and does
    /// not clone or mutate it.
    /// </remarks>
    public class BufferResponseModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the rule execution entries retained in the session buffer.
        /// </summary>
        /// <remarks>
        /// Initialized to an empty array so an empty buffer still serializes a stable,
        /// enumerable shape instead of a null member.
        /// </remarks>
        public BufferItem[] Buffer { get; set; } = [];
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents one rule execution entry retained in a session buffer.
        /// </summary>
        /// <remarks>
        /// The entry references the executed rule and does not clone or mutate it.
        /// </remarks>
        public sealed class BufferItem
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
        #endregion
    }
}
