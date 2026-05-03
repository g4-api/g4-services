using System.Text.Json;
using System.Text.Json.Serialization;

namespace G4.Models
{
    /// <summary>
    /// Represents a JSON-RPC request sent to an MCP endpoint.
    /// This model carries the request identifier, JSON-RPC protocol version,
    /// target method name, and the raw parameters payload.
    /// </summary>
    public class McpRequestModel
    {
        /// <summary>
        /// Gets or sets the request identifier.
        /// The identifier is used to correlate a response with its request.
        /// It can be a number, string, or another JSON-compatible scalar value.
        /// </summary>
        public object Id { get; set; }

        /// <summary>
        /// Gets or sets the JSON-RPC protocol version.
        /// This value defaults to <c>2.0</c>, which is the standard version used by JSON-RPC 2.0 requests.
        /// </summary>
        public string JsonRpc { get; set; } = "2.0";

        /// <summary>
        /// Gets or sets the name of the JSON-RPC method to invoke.
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Gets or sets the raw parameters payload for the JSON-RPC method.
        /// This property is mapped to the JSON field named <c>params</c> for both
        /// System.Text.Json and Newtonsoft.Json serialization.
        /// </summary>
        [JsonPropertyName(name: "params")]
        [Newtonsoft.Json.JsonProperty(propertyName: "params")]
        public JsonElement Parameters { get; set; }
    }
}