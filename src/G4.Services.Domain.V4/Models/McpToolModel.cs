using G4.Converters;

using ModelContextProtocol.Protocol;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace G4.Models
{
    /// <summary>
    /// Represents a tool available in the MCP (Model Context Protocol) system,
    /// including its name, description, and input schema.
    /// </summary>
    public class McpToolModel
    {
        #region *** Fields       ***
        // Static JSON serialization options with custom converters for consistent
        // serialization behavior across all instances of McpToolModel.
        private static readonly JsonSerializerOptions _jsonOptions = NewJsonOptions();
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the client tool used to interact with the MCP service.
        /// </summary>
        public Tool ClientTool { get; set; }

        /// <summary>
        /// Gets or sets the human-readable description of what this tool does.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the unique name of the tool.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the tool.
        /// </summary>
        [JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public ToolMetadataModel Metadata { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for this tool as defined in the G4 specification.
        /// </summary>
        [JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public string QualifiedName { get; set; }

        /// <summary>
        /// Gets or sets the type of tool. Default is "system-tool", indicating a system-defined tool.
        /// </summary>
        [JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public string Type { get; set; } = "system-tool";
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public override string ToString()
        {
            // Serialize the current instance using the default JSON serializer options.
            return ToString(_jsonOptions);
        }

        /// <summary>
        /// Returns the current instance serialized as JSON using the specified serializer options.
        /// </summary>
        /// <param name="options">The serializer options to use when converting the current instance to JSON.</param>
        /// <returns>A JSON string representation of the current instance.</returns>
        public string ToString(JsonSerializerOptions options)
        {
            // Serialize the current instance using the provided JSON serializer options.
            return JsonSerializer.Serialize(this, options);
        }

        // Creates a new instance of JsonSerializerOptions with custom settings and converters.
        private static JsonSerializerOptions NewJsonOptions()
        {
            // Initialize JSON serialization options.
            var jsonOptions = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            // Add a custom exception converter
            jsonOptions.Converters.Add(new ExceptionConverter());

            // Add a custom method base converter
            jsonOptions.Converters.Add(new MethodBaseConverter());

            // Add a custom type converter
            jsonOptions.Converters.Add(new TypeConverter());

            // Add a custom DateTime converter for ISO 8601 format (yyyy-MM-ddTHH:mm:ss.ffffffK)
            jsonOptions.Converters.Add(new DateTimeIso8601Converter());

            // Return the JSON options with custom settings and converters added
            return jsonOptions;
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents metadata information about a tool, including author, version,
        /// licensing, and reference details.
        /// </summary>
        public class ToolMetadataModel
        {
            /// <summary>
            /// Gets or sets the author of the tool (person, organization, or team).
            /// </summary>
            public string Author { get; set; }

            /// <summary>
            /// Gets or sets a human-readable description of the tool,
            /// summarizing its purpose or functionality.
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// Gets or sets the license under which the tool is distributed
            /// (e.g., MIT, Apache-2.0, proprietary).
            /// </summary>
            public string License { get; set; }

            /// <summary>
            /// Gets or sets the name of the tool.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the type of tool. Default is "system-tool", indicating a system-defined tool.
            /// </summary>
            [JsonIgnore, Newtonsoft.Json.JsonIgnore]
            public string Type { get; set; } = "system-tool";

            /// <summary>
            /// Gets or sets the URL associated with the tool,
            /// typically pointing to documentation, a repository, or a homepage.
            /// </summary>
            public string Url { get; set; }

            /// <summary>
            /// Gets or sets the version of the tool (e.g., "1.0.0").
            /// </summary>
            public string Version { get; set; }
        }
        #endregion
    }
}
