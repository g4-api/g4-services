using System;
using System.Collections.Generic;

namespace G4.Services.Mcp.Attributes
{
    /// <summary>
    /// Marks an implementation class as an MCP-exposed function and
    /// supplies the JSON-schema metadata that the hub publishes to callers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class McpFunctionAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="McpFunctionAttribute"/> class.
        /// </summary>
        public McpFunctionAttribute()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="McpFunctionAttribute"/> class.
        /// </summary>
        /// <param name="assembly">The full name of the assembly that contains the function implementation.</param>
        /// <param name="manifest">The file name (or embedded resource name) of the JSON manifest that describes the function’s parameters and schema.</param>
        public McpFunctionAttribute(string assembly, string manifest)
        {
            // Assembly name where the function is defined, e.g. "G4.Services.Mcp"
            // This is used to locate the function in the tool catalog.
            // Manifest is the name of the JSON file that contains the function's metadata.
        }

        /// <summary>
        /// JSON-schema <c>properties</c> map.<br/>
        /// Key = argument name; Value = argument definition.
        /// </summary>
        public Dictionary<string, Property> Arguments { get; set; }

        /// <summary>
        /// When <c>true</c>, the generated JSON schema allows additional
        /// properties beyond those specified in <see cref="Arguments"/>.
        /// </summary>
        public bool AdditionalProperties { get; set; }

        /// <summary>
        /// Logical category used for grouping the function in the tool catalog
        /// (e.g. <c>DataManagement</c>, <c>FileSystem</c>).
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Human-readable one-sentence description shown in UI listings.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Public name of the function as it will appear to LLM callers.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Names of parameters that must be present in every invocation.
        /// </summary>
        public string[] Required { get; set; }

        /// <summary>
        /// When <c>true</c>, the hub will ask the user for confirmation
        /// before executing the function.
        /// </summary>
        public bool RequiresConfirmation { get; set; }

        /// <summary>
        /// JSON-schema <c>type</c> of the generated parameter object.
        /// Typically <c>"object"</c>.
        /// </summary>
        public string Type { get; set; }

        #region *** Nested Types ***
        /// <summary>
        /// Describes a single argument in the function’s JSON schema.
        /// </summary>
        public sealed class Property
        {
            /// <summary>
            /// Default value assigned when the caller omits the parameter.
            /// </summary>
            public object Default { get; set; }

            /// <summary>
            /// Human-readable explanation of what the parameter does.
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// JSON-schema <c>type</c> for this parameter
            /// (e.g. <c>"string"</c>, <c>"integer"</c>, <c>"boolean"</c>).
            /// </summary>
            public string Type { get; set; }
        }
        #endregion
    }
}
