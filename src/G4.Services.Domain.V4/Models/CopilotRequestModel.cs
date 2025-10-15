using System.Text.Json;
using System.Text.Json.Serialization;

namespace G4.Models
{
    public class CopilotRequestModel
    {
        public object Id { get; set; }

        public string JsonRpc { get; set; } = "2.0";

        public string Method { get; set; }

        [JsonPropertyName(name: "params")]
        [Newtonsoft.Json.JsonProperty(propertyName: "params")]
        public JsonElement Parameters { get; set; }
    }
}
