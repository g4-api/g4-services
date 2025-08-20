using System.Collections.Generic;

namespace G4.Models
{
    /// <summary>
    /// Represents token-level log-probabilities returned by the OpenAI service.
    /// Includes the model’s probability estimates for each generated token
    /// and the alternative top tokens at each position.
    /// </summary>
    public class OpenAiLogProbs
    {
        /// <summary>
        /// Character offsets into the original prompt/completion text for each token.
        /// Can be used to align token probabilities with the source string.
        /// </summary>
        public List<int> TextOffset { get; set; } = [];

        /// <summary>
        /// The model’s log-probability for each generated token in the response.
        /// A null entry indicates that the probability was not provided for that token.
        /// </summary>
        public List<float?> TokenLogProbs { get; set; } = [];

        /// <summary>
        /// At each token position, a dictionary mapping alternative token strings
        /// to their log-probabilities. Useful for analyzing the model’s top choices.
        /// </summary>
        public List<Dictionary<string, float?>> TopLogProbs { get; set; } = [];
    }
}
