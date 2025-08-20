using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace G4.Models
{
    /// <summary>
    /// Represents a single entry in the streamed OpenAI response.
    /// Provides serialization to the Python-compatible JSON format.
    /// </summary>
    public class OpenAiStreamEntry
    {
        #region *** Properties   ***
        // Gets the default <see cref="JsonSerializerOptions"/> used for compatibility with Python-style JSON conventions.
        // Configured for lenient parsing, kebab-case property names, and case-insensitive matching.
        private static JsonSerializerOptions PythonOptions => new()
        {
            // Use kebab-case for property names during serialization (e.g., "some-value")
            PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,

            // Enable case-insensitive property matching during deserialization
            PropertyNameCaseInsensitive = true,

            // Allow trailing commas in JSON (for compatibility with some Python formats)
            AllowTrailingCommas = true,

            // Exclude null values when writing JSON to reduce payload size
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// The list of choices (deltas) returned by the model in this stream entry.
        /// </summary>
        public List<OpenAiStreamChoice> Choices { get; set; } = new();

        /// <summary>
        /// Unix timestamp (in seconds) when this entry was created by the service.
        /// </summary>
        public long Created { get; set; }

        /// <summary>
        /// The unique identifier for this stream entry.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The model name that generated this entry (e.g., "gpt-4o-mini").
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// The type of object returned (typically "chat.completion.chunk").
        /// </summary>
        public string Object { get; set; }

        /// <summary>
        /// The service tier used by the model (e.g., "default", "premium").
        /// </summary>
        public string ServiceTier { get; set; }

        /// <summary>
        /// Fingerprint identifying the system instance processing this request.
        /// </summary>
        public string SystemFingerprint { get; set; }

        /// <summary>
        /// Usage statistics for this stream entry, such as token counts.
        /// </summary>
        public OpenAiUsage Usage { get; set; }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc/>
        public override string ToString()
        {
            return ToString(jsonOptions: PythonOptions);
        }

        /// <summary>
        /// Serializes this entry to a "data: {json}\n\n" string using the provided options.
        /// </summary>
        /// <param name="jsonOptions">Options for JSON serialization.</param>
        /// <returns>A prefixed data string if serialization succeeds; otherwise an empty string.</returns>
        public string ToString(JsonSerializerOptions jsonOptions)
        {
            var json = JsonSerializer.Serialize(this, jsonOptions);
            return string.IsNullOrEmpty(json) ? string.Empty : $"data: {json}\n\n";
        }

        /// <summary>
        /// Converts a JSON string into an <see cref="OpenAiStreamEntry"/> using default (Python-friendly) options.
        /// </summary>
        /// <param name="input">The JSON string to convert.</param>
        /// <returns>An instance of <see cref="OpenAiStreamEntry"/> if deserialization is successful; otherwise, a new empty instance.</returns>
        public static OpenAiStreamEntry ConvertFromJson(string input)
        {
            // Call the overload with the default JSON options
            return ConvertFromJson(input, PythonOptions);
        }

        /// <summary>
        /// Attempts to deserialize a JSON string into an <see cref="OpenAiStreamEntry"/> using custom serialization options.
        /// </summary>
        /// <param name="input">The JSON string to convert.</param>
        /// <param name="jsonOptions">Options controlling the deserialization behavior.</param>
        /// <returns>A populated <see cref="OpenAiStreamEntry"/> if deserialization is successful; otherwise, a new empty instance.</returns>
        public static OpenAiStreamEntry ConvertFromJson(string input, JsonSerializerOptions jsonOptions)
        {
            try
            {
                // Attempt to deserialize using the specified options
                return JsonSerializer.Deserialize<OpenAiStreamEntry>(input, jsonOptions) ?? new OpenAiStreamEntry();
            }
            catch
            {
                // On failure, return a new empty instance
                return new OpenAiStreamEntry();
            }
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents a single choice (delta) in the stream with its metadata.
        /// </summary>
        public sealed class OpenAiStreamChoice
        {
            /// <summary>
            /// The incremental message data for this choice.
            /// </summary>
            public OpenAiStreamMessage Delta { get; set; }

            /// <summary>
            /// Reason why this choice finished (e.g., "stop", "length").
            /// </summary>
            public string FinishReason { get; set; }

            /// <summary>
            /// Index of this choice among multiple returned choices.
            /// </summary>
            public int Index { get; set; }

            /// <summary>
            /// Token log-probabilities for this choice, if requested.
            /// Mapped from the "logprobs" JSON property.
            /// </summary>
            [JsonPropertyName(name: "logprobs")]
            public OpenAiLogProbs LogProbs { get; set; }
        }

        /// <summary>
        /// The structure of each streamed message delta.
        /// May include content, function calls, tool calls, etc.
        /// </summary>
        public sealed class OpenAiStreamMessage
        {
            /// <summary>
            /// The textual content of this delta, if any.
            /// </summary>
            public string Content { get; set; }

            /// <summary>
            /// A function call request embedded in this delta.
            /// </summary>
            public OpenAiFunctionCall FunctionCall { get; set; }

            /// <summary>
            /// The name associated with this message (for assistant messages).
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Any refusal metadata, if the model refused to comply.
            /// </summary>
            public object Refusal { get; set; }

            /// <summary>
            /// The role of the message (e.g., "assistant", "user").
            /// </summary>
            public string Role { get; set; }

            /// <summary>
            /// Any tool calls invoked by this message delta.
            /// </summary>
            public List<OpenAiToolCall> ToolCalls { get; set; } = new();
        }
        #endregion
    }
}
