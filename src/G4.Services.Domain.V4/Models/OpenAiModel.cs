namespace G4.Models
{
    /// <summary>
    /// Represents a single model returned by the OpenAI API, wrapped for G4 formatting.
    /// Includes metadata such as creation time, identifier, ownership, and type.
    /// </summary>
    public class OpenAiModel
    {
        /// <summary>
        /// Gets or sets the Unix timestamp (in seconds) when the model was created.
        /// </summary>
        public long Created { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the model (e.g., "gpt-4").
        /// May be prefixed with "g4-" to indicate internal routing.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets the object type, which is always "model" for OpenAI model listings.
        /// </summary>
        public string Object { get; } = "model";

        /// <summary>
        /// Gets or sets the owner of the model, such as "openai", "system", or a specific organization ID.
        /// </summary>
        public string OwnedBy { get; set; }
    }
}
