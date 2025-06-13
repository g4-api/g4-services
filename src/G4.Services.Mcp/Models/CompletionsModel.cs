using G4.Services.Mcp.Attributes;

using System.Collections.Generic;

namespace G4.Services.Mcp.Models
{
    /// <summary>
    /// Root object describing a single completion request/response exchange.
    /// </summary>
    public sealed class CompletionsModel
    {
        /// <summary>
        /// The body object that mirrors the request payload for the streaming call.
        /// </summary>
        public BodyModel Body { get; set; }

        /// <summary>
        /// The function call mode indicating how the model should handle function calls.
        /// </summary>
        public string FunctionCall { get; set; }

        /// <summary>
        /// The list of functions that the model can call, defined by the user.
        /// </summary>
        public McpFunctionAttribute[] Functions { get; set; }

        /// <summary>
        /// The chronological list of messages exchanged before the current call.
        /// </summary>
        public List<Message> Messages { get; set; }
        /// <summary>
        /// Identifier of the model or pipeline that should process the request.
        /// </summary>
        public string ModelId { get; set; }

        /// <summary>
        /// The text sent by the user at the top level of the payload.
        /// </summary>
        public string UserMessage { get; set; }

        #region *** Nested Types ***
        /// <summary>
        /// Represents a single chat message (either <c>user</c> or <c>assistant</c>).
        /// </summary>
        public sealed class Message
        {
            /// <summary>
            /// Human-readable content of the message.
            /// </summary>
            public string Content { get; set; }

            /// <summary>
            /// Originator of the message (e.g., <c>"user"</c> or <c>"assistant"</c>).
            /// </summary>
            public string Role { get; set; }
        }

        /// <summary>
        /// Encapsulates request-specific options and nested data for a streaming
        /// completion call.
        /// </summary>
        public sealed class BodyModel
        {
            /// <summary>
            /// Messages included in the streaming request (may overlap the root list).
            /// </summary>
            public List<Message> Messages { get; set; }

            /// <summary>
            /// The model or pipeline name to use for this streaming invocation.
            /// </summary>
            public string Model { get; set; }

            /// <summary>
            /// Indicates whether the server should send token deltas as a stream.
            /// </summary>
            public bool Stream { get; set; }

            /// <summary>
            /// Additional options controlling what metadata is returned with the stream.
            /// </summary>
            public StreamOptions StreamOptions { get; set; }

            /// <summary>
            /// Describes the authenticated user making the request.
            /// </summary>
            public User User { get; set; }
        }

        /// <summary>
        /// Options controlling the format and content of the streamed response.
        /// </summary>
        public sealed class StreamOptions
        {
            /// <summary>
            /// When <c>true</c>, usage statistics (token counts, etc.) are appended
            /// to the response.
            /// </summary>
            public bool IncludeUsage { get; set; }
        }

        /// <summary>
        /// Information about the caller, useful for auditing and authorization.
        /// </summary>
        public sealed class User
        {
            /// <summary>
            /// Contact e-mail address associated with the user.
            /// </summary>
            public string Email { get; set; }

            /// <summary>
            /// Unique identifier of the user in the calling system.
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            /// Friendly display name of the user.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Role or permission level of the user (e.g., <c>"admin"</c>).
            /// </summary>
            public string Role { get; set; }
        }
        #endregion
    }
}
