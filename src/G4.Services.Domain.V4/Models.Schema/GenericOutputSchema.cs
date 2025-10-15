using Swashbuckle.AspNetCore.Annotations;

namespace G4.Models.Schema
{
    /// <summary>
    /// Generic response schema for MCP tool operations.
    /// Provides a standardized way to return the status of the operation
    /// along with any associated result value.
    /// </summary>
    [SwaggerSchema(description: "Generic response schema that includes the operation status and an optional result value.")]
    public class GenericOutputSchema
    {
        /// <summary>
        /// Indicates the outcome of the operation.
        /// Typical values may include "success", "error", or "pending".
        /// </summary>
        [SwaggerSchema(description: "The operation status (e.g., 'success', 'error').")]
        public string Status { get; set; }

        /// <summary>
        /// The value or result produced by the operation.
        /// This can be any object type (e.g., string, JSON, or complex data structure).
        /// </summary>
        [SwaggerSchema(description: "The result or payload of the operation. Can be any object (string, number, JSON, etc.).")]
        public object Result { get; set; }
    }
}
