namespace G4.Models
{
    /// <summary>
    /// Tracks token usage for an OpenAI API request/response cycle.
    /// Provides counts for prompt tokens, completion tokens, and the combined total.
    /// </summary>
    public class OpenAiUsage
    {
        /// <summary>
        /// Number of tokens generated in the model’s completion.
        /// </summary>
        /// <remarks>
        /// This count excludes prompt tokens and reflects only the tokens in the assistant’s reply.
        /// </remarks>
        public int CompletionTokens { get; set; }

        /// <summary>
        /// Number of tokens consumed by the user’s prompt.
        /// </summary>
        /// <remarks>
        /// Includes tokens in the user query and any system or context messages supplied.
        /// </remarks>
        public int PromptTokens { get; set; }

        /// <summary>
        /// Sum of <see cref="PromptTokens"/> and <see cref="CompletionTokens"/>.
        /// </summary>
        /// <remarks>
        /// Useful for billing and usage analytics, as OpenAI charges per total tokens.
        /// </remarks>
        public int TotalTokens { get; set; }
    }
}
