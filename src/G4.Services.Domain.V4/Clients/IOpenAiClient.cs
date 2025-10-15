using G4.Models;

using Microsoft.AspNetCore.Http;

using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Clients
{
    public interface IOpenAiClient
    {
        #region *** Methods      ***
        /// <summary>
        /// Retrieves the available OpenAI models from the configured API endpoint.
        /// Returns a response object containing status, result data, and any error message.
        /// </summary>
        /// <returns>A task representing the asynchronous operation, with a result of <see cref="OpenAiModelsResponse"/>.</returns>
        Task<OpenAiModelsResponse> GetModelsAsync();

        /// <summary>
        /// Retrieves a list of OpenAI models from the configured endpoint,
        /// applies a prefix to each model ID, and returns a standardized response object.
        /// </summary>
        /// <param name="prefix">A string prefix to prepend to each model ID (e.g., "g4-").</param>
        /// <returns>A task representing the asynchronous operation, returning an <see cref="OpenAiModelsResponse"/> that includes the HTTP status code, model data, and any error message.</returns>
        Task<OpenAiModelsResponse> GetModelsAsync(string prefix);

        /// <summary>
        /// Sends a non-streaming chat completion request to the OpenAI API
        /// and returns the full response content as a string.
        /// </summary>
        /// <param name="completions">The model containing the user prompt and completion parameters.</param>
        /// <returns>A task representing the asynchronous operation, returning the raw response content as a string.</returns>
        /// <exception cref="HttpRequestException">Thrown if the response indicates a non-successful status code.</exception>
        Task<string> SendCompletionsAsync(OpenAiChatCompletionRequest completions);

        /// <summary>
        /// Sends a streaming chat completion request to the OpenAI API and relays the response line-by-line
        /// to the client via the HTTP response stream using Server-Sent Events (SSE).
        /// </summary>
        /// <param name="httpResponse">The HTTP response object used to stream data back to the client.</param>
        /// <param name="completions">The model containing the user's request details for the completion.</param>
        /// <returns>A task that represents the asynchronous streaming operation.</returns>
        Task SendCompletionsStreamAsync(HttpResponse httpResponse, OpenAiChatCompletionRequest completions);
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents the result of an OpenAI model list request,
        /// including status code, error message, and response payload.
        /// </summary>
        public sealed class OpenAiModelsResponse
        {
            /// <summary>
            /// Gets or sets the error message returned from the OpenAI API, if any.
            /// Will be empty or null if the request was successful.
            /// </summary>
            public string Error { get; set; }

            /// <summary>
            /// Gets or sets the deserialized list of models returned by the OpenAI API.
            /// </summary>
            public OpenAiModelListResponse Response { get; set; }

            /// <summary>
            /// Gets or sets the HTTP status code returned by the OpenAI API request.
            /// </summary>
            public int StatusCode { get; set; }
        }
        #endregion
    }
}
