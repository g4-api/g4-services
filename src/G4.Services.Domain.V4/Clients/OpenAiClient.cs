using G4.Extensions;
using G4.Models;
using G4.Settings;

using Microsoft.AspNetCore.Http;

using System;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Clients
{
    internal class OpenAiClient(IHttpClientFactory httpClientFactory) : IOpenAiClient
    {
        // Strongly-typed HTTP client created via the factory, configured for OpenAI requests
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient(name: "openai");

        // Settings model containing OpenAI API configuration, injected via constructor
        private readonly OpenAiSettingsModel _settings = AppSettings.OpenAi;

        /// <inheritdoc />
        public Task<IOpenAiClient.OpenAiModelsResponse> GetModelsAsync()
        {
            return GetModelsAsync("g4-");
        }

        /// <inheritdoc />
        public async Task<IOpenAiClient.OpenAiModelsResponse> GetModelsAsync(string prefix)
        {
            // Construct the full request URL by appending /models to the OpenAI endpoint
            var requestUri = $"{_settings.ClientOptions.Endpoint.TrimEnd('/')}/models";

            // Build the GET request with Authorization header using the API key
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri)
                .SetAuthorization(scheme: "Bearer", parameter: _settings.ApiKey);

            // Send the request and wait for the HTTP response
            var response = await _httpClient.SendAsync(request);

            // If the response is unsuccessful, return the status and raw error content
            if (!response.IsSuccessStatusCode)
            {
                return new()
                {
                    StatusCode = (int)response.StatusCode,
                    Response = new OpenAiModelListResponse(),
                    Error = await response.Content.ReadAsStringAsync()
                };
            }

            // Read the response content as a raw JSON string
            var jsonContent = await response.Content.ReadAsStringAsync();

            // Deserialize the JSON into a strongly-typed model list using configured options
            var models = JsonSerializer.Deserialize<OpenAiModelListResponse>(jsonContent, AppSettings.OpenAiJsonOptions);

            // Apply the provided prefix to each model ID
            foreach (var model in models.Data)
            {
                model.Id = $"{prefix}{model.Id}";
            }

            // Return a successful result with the transformed model list
            return new()
            {
                StatusCode = StatusCodes.Status200OK,
                Response = models,
                Error = string.Empty
            };
        }

        /// <inheritdoc />
        public async Task<string> SendCompletionsAsync(OpenAiChatCompletionRequest completions)
        {
            // Build the HTTP request using the provided completion model and application settings
            var request = NewCompletionsRequest(completions);

            // Send the request and wait for the entire content to be received
            var response = await _httpClient.SendAsync(
                request,
                completionOption: HttpCompletionOption.ResponseContentRead);

            //Throw an exception if the HTTP response is not successful(4xx or 5xx)
            response.EnsureSuccessStatusCode();

            // Read and return the entire response body as a string
            return await response.Content.ReadAsStringAsync();
        }

        /// <inheritdoc />
        public async Task SendCompletionsStreamAsync(HttpResponse httpResponse, OpenAiChatCompletionRequest completions)
        {
            // Set the default tool choice to "auto" for automatic tool selection
            completions.ToolChoice = completions.Tools?.Any() == true
                ? "auto"
                : null;

            // Build the OpenAI streaming request using app settings and user-provided prompt
            var request = NewCompletionsRequest(completions);

            // Send the request and start streaming as soon as headers are received
            var response = await _httpClient.SendAsync(
                request,
                completionOption: HttpCompletionOption.ResponseHeadersRead);

            // Set up response headers for Server-Sent Events (SSE)
            httpResponse.StatusCode = (int)response.StatusCode;
            httpResponse.ContentType = "text/event-stream";
            httpResponse.Headers.CacheControl = "no-cache";
            httpResponse.Headers["X-Accel-Buffering"] = "no";     // Disable buffering for reverse proxies (e.g., Nginx)
            httpResponse.Headers.XContentTypeOptions = "nosniff"; // Prevent MIME-type sniffing

            // Open the response stream from OpenAI and prepare readers/writers
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(responseStream);
            await using var writer = new StreamWriter(httpResponse.Body);

            // Initialize a variable to hold each line read from the OpenAI stream
            string line;

            //var toolStreams = new Dictionary<string, List<OpenAiStreamEntry>>();
            var toolStreams = new List<string>();

            // Set a flag to indicate the start of the thinking state
            var startOfThinking = true;

            // Relay each line from the OpenAI stream to the HTTP response
            while ((line = await reader.ReadLineAsync()) != null)
            {
                await File.AppendAllTextAsync("C:\\temp\\log-not-working.txt", line + Environment.NewLine);

                if (startOfThinking)
                {
                    await InvokeCot(writer, line);
                    startOfThinking = false;
                }

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // Write the line to the client stream and flush immediately
                await writer.WriteLineAsync(line);
                await writer.FlushAsync();
            }

            // Final flush to ensure all data is pushed out
            await writer.FlushAsync();
            await httpResponse.Body.FlushAsync();
        }

        // Constructs an HttpRequestMessage to send a chat completion request to the OpenAI API, including tools and model cleanup.
        private static HttpRequestMessage NewCompletionsRequest(OpenAiChatCompletionRequest completions)
        {
            // Clean up the model name by removing any "g4-" prefix (used internally)
            completions.Model = completions
                .Model?
                .Replace("g4-", string.Empty, StringComparison.OrdinalIgnoreCase);

            // Construct the full request URI for OpenAI's /chat/completions endpoint
            var requestUri = $"{AppSettings.OpenAi.ClientOptions.Endpoint.TrimEnd('/')}/chat/completions";

            // Retrieve the OpenAI API key from the app settings
            var apiKey = AppSettings.OpenAi.ApiKey;

            // Serialize the completions payload using the configured JSON options
            var content = JsonSerializer.Serialize(completions, AppSettings.OpenAiJsonOptions);

            // Wrap the serialized content in a StringContent with application/json media type
            var stringContent = new StringContent(content, Encoding.UTF8, MediaTypeNames.Application.Json);

            // Construct the HTTP POST request with the target URI and request body
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = stringContent
            };

            // Set the Authorization header using the Bearer scheme and the API key
            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Return the fully built HTTP request ready to be sent
            return httpRequestMessage;
        }

        private static async Task InvokeCot(StreamWriter writer, string line)
        {
            OpenAiStreamEntry _refernceEntry;

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            // Trigger the "thinking" state by modifying the content of the first choice
            _refernceEntry = OpenAiStreamEntry.ConvertFromJson(line[6..]);
            _refernceEntry.Choices[0].Delta.Content = "<think>";

            await writer.WriteLineAsync(_refernceEntry.ToString());
            await writer.FlushAsync();

            for (var i = 0; i < 5; i++)
            {
                // Simulate some thinking process by updating the content with a delay
                _refernceEntry.Choices[0].Delta.Content = $"thinking {i + 1}...";
                _refernceEntry.Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                await writer.WriteLineAsync(_refernceEntry.ToString());
                await writer.FlushAsync();
                await Task.Delay(1000);
            }

            // Complete the "thinking" state by updating the content again
            _refernceEntry.Choices[0].Delta.Content = "</think>";
            _refernceEntry.Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            await writer.WriteLineAsync(_refernceEntry.ToString());
            await writer.FlushAsync();

        }
    }
}
