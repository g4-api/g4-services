using System.Collections.Generic;

namespace G4.Models
{
    /// <summary>
    /// Represents the response from the OpenAI service when listing available models.
    /// Contains a collection of <see cref="OpenAiModel"/> entries and a constant object type.
    /// </summary>
    public class OpenAiModelListResponse
    {
        /// <summary>
        /// The collection of models returned by the service.
        /// </summary>
        public List<OpenAiModel> Data { get; set; } = [];

        /// <summary>
        /// The type identifier for this response. Always "list" for model listings.
        /// </summary>
        public string Object { get; } = "list";
    }
}
