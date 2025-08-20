using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace G4.Models
{
    /// <summary>
    /// Represents a chat completion request sent to the OpenAI API.
    /// Includes configuration for model selection, behavior tuning, streaming, and tool invocation.
    /// </summary>
    public sealed class OpenAiChatCompletionRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Penalizes new tokens based on their frequency in the prompt so far.
        /// Higher values decrease the likelihood of repeating the same line.
        /// </summary>
        public float? FrequencyPenalty { get; set; }

        /// <summary>
        /// Optional per-request headers (not sent to OpenAI directly).
        /// Can be used by middleware or extensions for tracking or customization.
        /// </summary>
        public object Headers { get; set; }

        /// <summary>
        /// Adjusts the likelihood of specific tokens appearing in the output.
        /// Key = token ID, Value = bias adjustment (-100 to 100).
        /// </summary>
        public Dictionary<int, float> LogitBias { get; set; }

        /// <summary>
        /// If true, returns the log probabilities of the top tokens at each position.
        /// </summary>
        [JsonPropertyName("logprobs")]
        public bool? LogProbs { get; set; }

        /// <summary>
        /// Maximum number of tokens to generate in the response.
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// The chronological list of messages exchanged before the current call.
        /// Used to provide conversational context to the model.
        /// </summary>
        public List<Message> Messages { get; set; }

        /// <summary>
        /// Identifier of the model or pipeline that should process the request.
        /// Examples: "gpt-4", "gpt-3.5-turbo", or custom routing tags.
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// Penalizes new tokens based on whether they appear in the text so far.
        /// Encourages diversity by reducing repetition.
        /// </summary>
        public float? PresencePenalty { get; set; }

        /// <summary>
        /// Format in which the model should return the response (e.g., "text", "json").
        /// Accepts custom values depending on downstream logic.
        /// </summary>
        public object ResponseFormat { get; set; }

        /// <summary>
        /// Seed value used for deterministic outputs (when supported by the model).
        /// </summary>
        public int? Seed { get; set; }

        /// <summary>
        /// Defines stop sequences that will cause the model to stop generating further tokens.
        /// Can be a string, array, or null.
        /// </summary>
        public object Stop { get; set; }

        /// <summary>
        /// Indicates whether the model should return partial token deltas as a stream.
        /// If true, the response is streamed using Server-Sent Events (SSE).
        /// </summary>
        public bool Stream { get; set; }

        /// <summary>
        /// Additional configuration for streaming behavior (if supported).
        /// </summary>
        public object StreamOptions { get; set; }

        /// <summary>
        /// Sampling temperature: higher values (e.g., 0.9) make output more random;
        /// lower values (e.g., 0.2) make output more deterministic.
        /// </summary>
        public float? Temperature { get; set; }

        /// <summary>
        /// List of tools (functions or plugins) made available to the model for this completion.
        /// Typically includes type/function descriptor pairs.
        /// </summary>
        public IEnumerable<object> Tools { get; set; }

        /// <summary>
        /// Optional tool name or special keyword ("auto", "none") directing how tool selection should be handled.
        /// </summary>
        public string ToolChoice { get; set; }

        /// <summary>
        /// Number of top tokens to include when <c>logprobs</c> is enabled.
        /// </summary>
        [JsonPropertyName("top_logprobs")]
        public int? TopLogProbs { get; set; }

        /// <summary>
        /// Controls diversity via nucleus sampling.
        /// Model considers the smallest set of tokens whose cumulative probability exceeds this value.
        /// </summary>
        public float? TopP { get; set; }

        /// <summary>
        /// A unique identifier representing the end user.
        /// Can be used for rate limiting, analytics, or tracking purposes.
        /// </summary>
        public string User { get; set; }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents a single message exchanged in a chat conversation,
        /// including role, content, and optional tool usage context.
        /// </summary>
        public sealed class Message
        {
            /// <summary>
            /// Human-readable content of the message.
            /// For example: a user query or assistant reply.
            /// </summary>
            public string Content { get; set; }

            /// <summary>
            /// Optional name associated with the message.
            /// Typically used in function calls or system messages.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Originator of the message.
            /// Valid values include <c>"user"</c>, <c>"assistant"</c>, <c>"system"</c>, or <c>"tool"</c>.
            /// </summary>
            public string Role { get; set; }

            /// <summary>
            /// Identifier of the tool call this message is responding to (if applicable).
            /// Used when replying to a previous function/tool invocation.
            /// </summary>
            public string ToolCallId { get; set; }

            /// <summary>
            /// A list of tool calls initiated by the assistant in this message.
            /// Each item typically contains a function name, arguments, and a unique call ID.
            /// </summary>
            public List<object> ToolCalls { get; set; }
        }
        #endregion
    }
}
