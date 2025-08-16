using G4.Api;
using G4.Cache;
using G4.Converters;
using G4.Extensions;
using G4.Models;

using HtmlAgilityPack;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Repository for handling Copilot JSON-RPC operations:
    /// manages session state, available tools (both plugin-based and system tools),
    /// and dispatches initialization and invocation requests.
    /// </summary>
    /// <param name="clientFactory">Factory to create <see cref="HttpClient"/> instances with named configurations.</param>
    /// <param name="cache">The <see cref="CacheManager"/> instance containing plugin caches.</param>
    /// <param name="client">The <see cref="G4Client"/> used for any external service interactions.</param>
    public class CopilotRepository(
        IHttpClientFactory clientFactory,
        CacheManager cache,
        G4Client client) : ICopilotRepository
    {
        #region *** Constants    ***
        // The JSON-RPC protocol version used in all responses.
        private const string JsonRpcVersion = "2.0";
        #endregion

        #region *** Fields       ***
        // Static, atomically swappable snapshot
        private static ConcurrentDictionary<string, McpToolModel> _tools =
            FormatTools(CacheManager.Instance);

        // Tracks active browser or agent sessions by session ID.
        private static readonly ConcurrentDictionary<object, object> _sessions = [];

        // Cache manager providing access to plugin manifests.
        private readonly CacheManager _cache = cache;

        // Client for external G4 API calls, if needed by certain tools.
        private readonly G4Client _client = client;

        // HTTP client configured for OpenAI API interactions, using a named configuration.
        private readonly HttpClient _httpClient = clientFactory.CreateClient(name: "copilot-openai");
        #endregion

        #region *** Properties   ***
        // JSON serialization options for consistent formatting and naming conventions.
        private static JsonSerializerOptions JsonOptions
        {
            get
            {
                // Create a fresh options instance.
                // If this is on a hot path, consider caching in a static readonly field to avoid per-call allocations.
                var options = new JsonSerializerOptions
                {
                    // Do not write properties whose value is null.
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

                    // Serialize dictionary keys in snake_case (e.g., "error_code").
                    DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,

                    // Serialize CLR property names in snake_case (e.g., "request_id").
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,

                    // Compact output optimized for transport/log size. Set true for dev readability.
                    WriteIndented = false
                };

                // Serialize System.Type in a stable, readable format.
                options.Converters.Add(new TypeConverter());

                // Normalize exception payloads (type, message, stack trace, etc.).
                options.Converters.Add(new ExceptionConverter());

                // Enforce ISO-8601 DateTime text to avoid locale/round-trip issues.
                options.Converters.Add(new DateTimeIso8601Converter());

                // Provide a readable/portable representation for MethodBase (useful in logs/telemetry).
                options.Converters.Add(new MethodBaseConverter());

                // Return the configured options.
                return options;
            }
        }

        private static JsonSerializerOptions G4JsonOptions
        {
            get
            {
                // Create a fresh options instance.
                // If this is on a hot path, consider caching in a static readonly field to avoid per-call allocations.
                var options = new JsonSerializerOptions
                {
                    // Do not write properties whose value is null.
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

                    // Serialize dictionary keys in snake_case (e.g., "error_code").
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,

                    // Serialize CLR property names in snake_case (e.g., "request_id").
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

                    // Compact output optimized for transport/log size. Set true for dev readability.
                    WriteIndented = false
                };

                // Serialize System.Type in a stable, readable format.
                options.Converters.Add(new TypeConverter());

                // Normalize exception payloads (type, message, stack trace, etc.).
                options.Converters.Add(new ExceptionConverter());

                // Enforce ISO-8601 DateTime text to avoid locale/round-trip issues.
                options.Converters.Add(new DateTimeIso8601Converter());

                // Provide a readable/portable representation for MethodBase (useful in logs/telemetry).
                options.Converters.Add(new MethodBaseConverter());

                // Return the configured options.
                return options;
            }
        }

        // Registry of all available tools by name.
        private static readonly ConcurrentDictionary<string, McpToolModel> Tools
            = Volatile.Read(ref _tools);
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public CopilotToolsResponseModel FindTool(string toolName, object id)
        {
            // Guard against invalid input early to produce a clear failure mode.
            if (string.IsNullOrWhiteSpace(toolName))
            {
                throw new ArgumentException("Tool name must be provided.", nameof(toolName));
            }

            // Build the JSON-RPC style envelope expected by FindG4Tool
            var envelope = new Dictionary<string, object>
            {
                ["arguments"] = new Dictionary<string, object>
                {
                    ["tool_name"] = toolName
                }
            };

            // Create a JsonElement directly (no Serialize→Deserialize string round-trip).
            // Uses your configured JsonOptions (snake_case, ignore nulls, etc.).
            var arguments = JsonSerializer.SerializeToElement(envelope, JsonOptions);

            // Delegate to the generic finder with the shared registry and protocol version.
            return FindG4Tool(
                tools: Tools,
                jsonrpcVersion: JsonRpcVersion,
                arguments,
                id);
        }

        /// <inheritdoc />
        public CopilotToolsResponseModel GetTools(object id) => GetG4Tools(Tools, id);

        /// <inheritdoc />
        public CopilotInitializeResponseModel Initialize(object id) => new()
        {
            // Set the request ID and JSON-RPC version for the response.
            Id = id,
            Jsonrpc = JsonRpcVersion,

            // Define the result, which contains the capabilities, protocol version, and server info.
            Result = new()
            {
                Capabilities = new()
                {
                    // Indicate that the tool list has changed (can be used to trigger updates or notifications).
                    Tools = new()
                    {
                        ListChanged = true
                    }
                },

                // The protocol version the server is using (fixed value in this case).
                ProtocolVersion = "2025-03-26",

                // Define the server information (name and version).
                ServerInfo = new()
                {
                    Name = "g4-engine-copilot-mcp",
                    Version = "4.0.0"
                }
            }
        };

        /// <inheritdoc />
        public CopilotToolsResponseModel InvokeTool(JsonElement parameters, object id)
        {
            // Extract the "arguments" object from the JSON parameters.
            // This contains tool-specific input such as driver settings, session IDs, etc.
            var arguments = parameters.GetProperty("arguments");

            // Retrieve the "driver" argument (e.g., ChromeDriver). Defaults to null if not provided.
            var driver = arguments.TryGetProperty("driver", out var driverOut)
                ? driverOut.GetString()
                : null;

            // Retrieve the "driver_binaries" argument (path or URL to driver binaries). Defaults to null if not provided.
            var driverBinaries = arguments.TryGetProperty("driver_binaries", out var driverBinariesOut)
                ? driverBinariesOut.GetString()
                : null;

            // Retrieve the "session" argument (existing session ID for reusing a browser session). Defaults to null if not provided.
            var driverSession = arguments.TryGetProperty("driver_session", out var sessionOut)
                ? sessionOut.GetString()
                : null;

            // Retrieve the "token" argument (authentication or API token). Defaults to null if not provided.
            var token = arguments.TryGetProperty("token", out var tokenOut)
                ? tokenOut.GetString()
                : null;

            // Retrieve the "name" of the tool to be invoked. Defaults to null if not provided.
            var toolName = parameters.TryGetProperty("name", out var toolNameOut)
                ? toolNameOut.GetString()
                : null;

            // Look up the tool definition in the registered tools dictionary.
            // If the tool is not found, 'tool' will be null and handled in the default branch below.
            var tool = Tools.GetValueOrDefault(toolName);

            // Match the tool by name and execute the corresponding handler.
            // Some tools are built-in system tools, others are dynamically loaded plugins.
            var result = tool switch
            {
                // Built-in: Finds and returns metadata about a tool by its name.
                { Name: "find_tool" } => FindG4Tool(Tools, JsonRpcVersion, arguments, id),

                // Built-in: Retrieves the current application's DOM (Document Object Model).
                { Name: "get_application_dom" } => GetApplicationDom(_client, _sessions, driverSession, token),

                // Built-in: Returns the instructions for the next tool call, including policies and defaults.
                { Name: "get_instructions" } => GetInstructions(),

                // Built-in: Retrieves the locator for a specific element on the page.
                { Name: "get_locator" } => GetLocator(_httpClient, _client, _sessions, driverSession, token, "https://api.openai.com/v1/chat/completions", "", arguments),

                // Built-in: Lists all available tools.
                { Name: "get_tools" } => GetG4Tools(Tools, id),

                // Built-in: Starts a new G4 browser automation session.
                { Name: "start_g4_session" } => StartG4Session(_client, _sessions, driver, driverBinaries, token),

                // Default: Assumes this is a plugin-based tool and converts parameters into an executable rule.
                _ => StartG4Rule(_client, _sessions, driverSession, token, rule: ConvertToRule(Tools, toolName, arguments))
            };

            // Construct and return the JSON-RPC response to the client.
            return new()
            {
                // Echo back the request ID so the caller can match responses to requests.
                Id = id,

                // JSON-RPC version identifier.
                Jsonrpc = JsonRpcVersion,

                // The result object contains both a text-serialized version and the structured data.
                Result = new
                {
                    // Plain text content (serialized JSON of the result object).
                    Content = new[]
                    {
                        new
                        {
                            Type = "text",
                            Text = JsonSerializer.Serialize(result, JsonOptions)
                        }
                    },

                    // The raw structured content (original object form of the result).
                    StructuredContent = result
                }
            };
        }

        /// <inheritdoc />
        public void SyncTools()
        {
            // Rebuild the tools collection from the cache manager.
            var rebuilt = new ConcurrentDictionary<string, McpToolModel>(FormatTools(_cache));

            // Atomically replace the current tools collection with the rebuilt one.
            Interlocked.Exchange(ref _tools, rebuilt);
        }

        // Searches for a G4 tool in the internal tool registry by its name. 
        // Returns a response containing the tool's details or an error message if the tool is not found.
        private static CopilotToolsResponseModel FindG4Tool(
            ConcurrentDictionary<string, McpToolModel> tools,
            string jsonrpcVersion,
            JsonElement arguments,
            object id)
        {
            // Extract the tool name from the arguments.
            var toolName = arguments.TryGetProperty("tool_name", out var toolNameOut)
                ? toolNameOut.GetString()
                : default;

            // Look up the tool by name in the internal registry.
            var tool = tools.GetValueOrDefault(toolName);

            // Initialize the response model with the provided ID and JSON-RPC version.
            var response = new CopilotToolsResponseModel
            {
                Id = id,
                Jsonrpc = jsonrpcVersion
            };

            // If the tool is not found, set the error details in the response.
            if (tool == null)
            {
                response.Error = new()
                {
                    // JSON-RPC error code for method not found.
                    Code = -32601,

                    // Provide a message indicating the tool is missing.
                    Message = $"Tool '{toolName}' not found."
                };
            }
            // If the tool is found, populate the result with the tool's details.
            else
            {
                response.Result = tool;
            }

            // Return the response, either with the tool data or an error.
            return response;
        }

        // Retrieves and cleans the DOM of a web page through the automation process by invoking a JavaScript script
        // to extract the HTML content and then processing it to remove unwanted elements.
        private static Dictionary<string, object> GetApplicationDom(
            G4Client client,
            ConcurrentDictionary<object, object> sessions,
            string driverSession,
            string token)
        {
            // JavaScript to get the entire HTML content of the document.
            var script = "return document.body.outerHTML;";

            // Create the action rule to register the document object output as a session parameter.
            var registerParameterRule = new ActionRuleModel
            {
                Argument = "{{$ --Name:DocumentObjectOutput --Value:{{$New-ScriptResult --Src:" + script + "}} --Scope:Session}}",
                PluginName = "RegisterParameter"
            };

            // Create the automation model and invoke it using the provided client.
            var automation = NewAutomation(driverSession, token, registerParameterRule);
            var response = client.Automation.Invoke(automation);

            // Extract the session information from the response.
            var session = response.Values.Last().Sessions.Last();
            sessions[session.Key] = session.Value; // Store the session in the dictionary for tracking.

            // Retrieve the document object (HTML content) stored as a session parameter and decode it from base64.
            var documentObject = session
                .Value
                .Environment
                .SessionParameters
                .Get(key: "DocumentObjectOutput", defaultValue: "<html></html>".ConvertToBase64())
                .ConvertFromBase64();

            // Import the HTML content and clean the document (removing unwanted tags/elements).
            var cleanHtml = new HtmlDocument()
                .ImportHtml(documentObject)
                .DocumentNode
                .Clean();

            // Return the cleaned DOM as part of an anonymous object.
            return new Dictionary<string, object>
            {
                ["driver_session"] = session.Key,
                ["value"] = cleanHtml.OuterHtml
            };
        }

        // Return a new object containing the instructions for the next tool call.
        private static object GetInstructions()
        {
            return new
            {
                PolicyVersion = "2025.08.13.1",
                CheckList = new[]
                {
                    "Read token from .env (ask if missing)",
                    "Ensure driver_session (start_g4_session if missing with WEB_DRIVER / WEB_DRIVER_REPOSITORY)",
                    "Run get_tools to confirm <toolName> exists",
                    "Run find_tool(<toolName>) and validate schema"

                },
                Defaults = new Dictionary<string, object>()
                {
                    ["start_g4_session.driver"] = "ENV:WEB_DRIVER",
                    ["start_g4_session.driver_binaries"] = "ENV:WEB_DRIVER_REPOSITORY",
                },
                Guards = new Dictionary<string, object>()
                {
                    ["no_guessing"] = true,
                    ["retry_find_tool_once"] = true,
                    ["on_locator_failure"] = "ask_user"
                },
                //Must = new Dictionary<string, object>()
                //{
                //    ["prepend_steps"] = new[]
                //    {
                //        "Always fetch `token` from `.env` `G4_API_TOKEN` (ask user if missing)",
                //        "If no `driver_session`, call `start_g4_session` with `.env` defaults: `WEB_DRIVER` → `driver`, `WEB_DRIVER_REPOSITORY` → `driver_binaries`; save session id",
                //        "**Before every tool call**, first call `get_instructions` and **apply** its returned policy for the *next* tool call.",
                //        "Then: `get_tools` → verify tool → `find_tool` → validate input schema.",
                //        "For all tools **except** `start_g4_session`, include `driver_session` + `token`.",
                //        "If page interaction, call get_locator {intent:'click login button', action:'click'} and use primary locator.",
                //        "Sequence per call: `get_instructions` → token → driver\\_session → get\\_tools → find\\_tool → locator (if needed) → build per schema → add session+token → call tool.",
                //        "Never invent tool names/params/locators; if missing, ask."
                //    }
                //},
                PageTool= new Dictionary<string, object>()
                {
                    ["required"] = "ONLY when interacting with the page",
                    ["steps"] = new[]
                    {
                        "Call get_locator { intent:'<brief action>', action:'<click|type|select|read|...>', hints:{...}, constraints:{...} }",
                        "Use ONLY the returned primary locator (or explicit fallback) — never guess"
                    }
                },
                Params = new Dictionary<string, object>()
                {
                    ["must_add"] = new[] { "driver_session", "token" },
                    ["except_tools"] = new[] { "start_g4_session" }
                },
                TtlSeconds = 60
            };
        }

        // Retrieves the list of all available G4 tools from the internal registry and returns them in a JSON-RPC response model.
        private static CopilotToolsResponseModel GetG4Tools(
            ConcurrentDictionary<string, McpToolModel> tools,
            object id)
        {
            // Return a new CopilotToolsResponseModel with the list of tools from the registry.
            return new()
            {
                // Include the request ID to correlate the response with the request.
                Id = id,
                Jsonrpc = JsonRpcVersion,
                Result = new CopilotToolsResponseModel.ResultModel()
                {
                    // Provide the list of tools contained in the registry as the result.
                    Tools = tools.Values
                }
            };
        }

        // Retrieves the locator for a specific element on the page by sending a request to the OpenAI API.
        private static string GetLocator(
            HttpClient httpClient,
            G4Client client,
            ConcurrentDictionary<object, object> sessions,
            string driverSession,
            string token,
            string openAiUri,
            string openAiApiKey,
            JsonElement intent)
        {
            var documentObject = GetApplicationDom(client, sessions, driverSession, token);

            var intentObject = JsonSerializer.Deserialize<Dictionary<string, object>>(intent.GetRawText(), JsonOptions);
            intentObject["dom"] = documentObject["value"];

            // Send request to openai api completion API to get the locator.
            var payload = new
            {
                Model = "gpt-4o-mini",
                Messages = new[]
                {
                    new
                    {
                        Role = "system",
                        Content = File.ReadAllText("C:\\temp\\locator-system-prompt.txt")
                    },
                    new
                    {
                        Role = "user",
                        Content = JsonSerializer.Serialize(intentObject, JsonOptions)
                    }
                }
            };

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(openAiUri)
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openAiApiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            // Send the request and get the response.
            var response = httpClient.Send(request);
            var body = response.Content.ReadAsStringAsync().Result;
            var a = "";

            return body;

        }

        // Starts the automation rule execution by invoking the specified rule on the client, 
        // and retrieves the response, including the driver session and the value of the executed rule.
        private static object StartG4Rule(
            G4Client client,
            ConcurrentDictionary<object, object> sessions,
            string driverSession,
            string token,
            G4RuleModelBase rule)
        {
            // Create a new automation model using the provided driver binaries, authentication token, and rule.
            var automation = NewAutomation(driverSession, token, rule);

            // Invoke the automation process using the client and retrieve the response.
            var response = client.Automation.Invoke(automation);

            // Extract the session object from the response, ensuring we track the latest session.
            var session = response.Values.Last().Sessions.Last();

            // Add the session to the sessions dictionary to keep track of the active session.
            sessions[session.Key] = session.Value;

            // Retrieve the response for the rule execution from the response tree structure.
            var ruleResponse = session
                .Value
                .ResponseTree
                .Stages.Last()
                .Jobs.Last()
                .Plugins.Last();

            // Return an anonymous object containing the session key and the rule response value.
            return new
            {
                DriverSession = session.Key,
                Value = ruleResponse
            };
        }

        // Starts a new G4 session by invoking the automation process with the provided parameters.
        // The session information is returned, and the session is added to the provided dictionary of active sessions.
        private static object StartG4Session(
            G4Client client,
            ConcurrentDictionary<object, object> sessions,
            string driver,
            string driverBinaries,
            string token)
        {
            // Define the driver parameters, including the driver name and the path to the binaries.
            // The driver name (e.g., Chrome, Firefox).
            // The path to the driver binaries or the Selenium Grid URL.
            var driverParameters = new Dictionary<string, object>
            {
                ["driver"] = driver,
                ["driverBinaries"] = driverBinaries
            };

            // Create the automation model which includes the authentication and driver parameters.
            var automation = NewAutomation(driverSession: default, token);

            // Set the driver parameters for the automation model.
            // This dictionary contains configuration for the browser driver (e.g., driver name and path).
            // These parameters are required for the automation process to know which browser and driver binaries to use.
            automation.DriverParameters = driverParameters;

            // Invoke the automation process and get the response.
            var response = client.Automation.Invoke(automation);

            // Retrieve the session information from the response.
            var session = response.Values.Last().Sessions.Last();

            // Add the session to the sessions dictionary using the session key.
            sessions[session.Key] = session.Value;

            // Return an anonymous object containing the session key.
            // The session key for the newly created session.
            return new
            {
                DriverSession = session.Key
            };
        }

        // TODO: Implement logic to handle all types of G4 rules.
        // Converts the given tool name and parameters into a G4 rule model.
        // This method retrieves the plugin name associated with the tool and uses the parameters to create a rule model.
        private static ActionRuleModel ConvertToRule(
            ConcurrentDictionary<string, McpToolModel> tools,
            string toolName,
            JsonElement arguments)
        {
            // Formats a JSON parameters object into a templated, G4 CLI-style string,
            static string FormatParameters(JsonElement parameters)
            {
                if (parameters.ValueKind != JsonValueKind.Object)
                {
                    // If the parameters are not an object, return an empty string.
                    return string.Empty;
                }

                // Build a case-insensitive dictionary of name → textual value.
                // $"{i.Value}" calls JsonElement.ToString() (see boolean casing note above).
                var parametersObject = parameters
                    .EnumerateObject()
                    .ToDictionary(i => i.Name, i => $"{i.Value}", StringComparer.OrdinalIgnoreCase);

                // Round-trip through System.Text.Json to apply G4JsonOptions policies (e.g., snake_case keys).
                var json = JsonSerializer.Serialize(parametersObject, G4JsonOptions);
                var parametersCollection = JsonSerializer.Deserialize<Dictionary<string, string>>(json, G4JsonOptions)!;

                // Convert keys to PascalCase and render as --Key[:Value] (omit :Value when empty).
                var parametersExpression = parametersCollection
                    .Select(i =>
                        $"--{i.Key.ConvertToPascalCase()}" +
                        (string.IsNullOrEmpty(i.Value) ? "" : $":{i.Value}"))
                    .ToArray();

                // Wrap with G4 template delimiters.
                return "{{$ " + string.Join(" ", parametersExpression) + "}}";
            }

            // Retrieve the plugin name associated with the provided tool name from the internal tool registry (_tools)
            var pluginName = tools.GetValueOrDefault(key: toolName)?.G4Name;

            // Get the parameters from the arguments, defaulting to an empty JSON object if not provided.
            var parameters = arguments.TryGetProperty("parameters", out var parametersOut)
                ? parametersOut
                : JsonDocument.Parse("{}").RootElement;

            // Format the parameters into a command-line style string (e.g., "--param1:value1 --param2:value2").
            var parametersCli = FormatParameters(parameters);

            // Get the properties from the arguments, defaulting to a JSON object with the plugin name if not provided.
            var properties = arguments.TryGetProperty("properties", out var propertiesOut)
                ? propertiesOut.GetRawText()
                : "{\"plugin_name\":\"" + pluginName + "\"}";

            // Serialize the parameters into a JSON string to prepare them for deserialization into a G4RuleModelBase
            //var json = JsonSerializer.Serialize(properties);

            // Deserialize the JSON string into the G4RuleModelBase object using the provided JSON options (JsonOptions)
            var rule = JsonSerializer.Deserialize<ActionRuleModel>(properties, JsonOptions);

            // Set the PluginName property of the rule to the retrieved plugin name
            rule.PluginName = pluginName;

            // Set the Argument property of the rule to the formatted parameters if provided
            rule.Argument = string.IsNullOrEmpty(parametersCli)
                ? rule.Argument
                : parametersCli;

            // Return the created rule
            return rule;
        }

        // Creates a new G4 automation model with the provided session details and rule parameters.
        // This model will be used to execute automation tasks within the G4 framework, using the given driver session,
        // authentication token, and specified rules.
        private static G4AutomationModel NewAutomation(
            string driverSession,
            string token,
            params G4RuleModelBase[] rules)
        {
            // Create an authentication model using the provided token.
            // This token will be used to authenticate the automation session.
            // The authentication token is required for session authorization.
            var authentication = new AuthenticationModel
            {
                Token = token
            };

            // Define the driver parameters, using the session ID to identify the browser session.
            // The session ID is used to link to the specific browser session.
            var driverParameters = new Dictionary<string, object>
            {
                ["driver"] = $"Id({driverSession})"
            };

            // Define the environment settings, specifying that the environment details should be returned.
            // Ensures the environment details are returned as part of the automation result.
            var environmentsSettings = new EnvironmentsSettingsModel
            {
                ReturnEnvironment = true
            };

            // Create and return the new G4 automation model.
            return new G4AutomationModel
            {
                Authentication = authentication,
                DriverParameters = driverParameters,
                Settings = new()
                {
                    EnvironmentsSettings = environmentsSettings
                },
                // Define the stages of the automation process.
                Stages =
                [
                    new()
                    {
                        // Define the jobs to be executed within the stage.
                        Jobs =
                        [
                            new()
                            {
                                // Assign the provided rules to the job.
                                Rules = rules
                            }
                        ]
                    }
                ]
            };
        }

        // Formats the tools available in the G4 framework, combining both
        // plugin-based tools and built-in system tools.
        private static ConcurrentDictionary<string, McpToolModel> FormatTools(CacheManager cache)
        {
            // Retrieves an array of system tools from the <see cref="SystemTools"/> class,
            // filtering those that have the <see cref="SystemToolAttribute"/> applied.
            static McpToolModel[] GetSystemTools()
            {
                // Use reflection to retrieve all properties from the SystemTools class
                // Filter properties that have the SystemToolAttribute applied
                return [.. typeof(SystemTools).GetProperties()
                    .Where(i => i.GetCustomAttributes(typeof(SystemToolAttribute), false).Length != 0)
                    .Select(i => i.GetValue(null) as McpToolModel)
                    .Where(i => i != null)];
            }

            // Extract all plugin manifests from the cache
            var actions = cache
                .PluginsCache
                .Values
                .SelectMany(i => i.Values)
                .Where(i => i.Manifest.PluginType == "Action" && i.Manifest.Key != "NoAction")
                .Select(i => i.Manifest)
                .DistinctBy(manifest => manifest.Key);//.Take(0);

            // Convert each action manifest into an MCP tool model
            var tools = actions
                .Select(manifest => manifest.ConvertToTool())
                .ToList();

            // Retrieve built-in system tools (e.g., start browser)
            var systemTools = GetSystemTools();

            // Create a concurrent dictionary to hold all tools, ensuring thread-safe access.
            var toolsCollection = new ConcurrentDictionary<string, McpToolModel>(StringComparer.OrdinalIgnoreCase);

            // Combine plugin-based tools with system tools and populate the registry
            foreach (var tool in tools.Concat(systemTools).ToList())
            {
                // Add or overwrite entry by tool name
                toolsCollection[tool.Name] = tool;
            }

            // Return the populated tools collection.
            return toolsCollection;
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents a collection of system tools that are joined into the G4 framework.
        /// </summary>
        private static class SystemTools
        {
            /// <summary>
            /// Represents a system tool that retrieves the metadata and schema for a specific tool by its unique name.
            /// This tool provides detailed information about the tool, including its input/output schema, name, and description.
            /// </summary>
            [SystemTool(name: "find_tool")]
            public static McpToolModel FindTool => new()
            {
                /// <summary>
                /// The unique name of the tool, used to identify it within the system.
                /// </summary>
                Name = "find_tool",

                /// <summary>
                /// A brief description of what the tool does.
                /// This tool retrieves the metadata and schema for a specific tool identified by its unique name.
                /// </summary>
                Description = "Retrieves the metadata and schema for a specific tool by its unique name.",

                /// <summary>
                /// Defines the input schema for the tool, including the types and descriptions of input parameters.
                /// </summary>
                InputSchema = new()
                {
                    /// <summary>
                    /// The data type for the input parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of input parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["tool_name"] = new()
                        {
                            Type = ["string"],
                            Description = "The unique identifier of the tool to find."
                        }
                    },

                    /// <summary>
                    /// A list of required input parameters that must be provided for the tool to execute successfully.
                    /// </summary>
                    Required = ["tool_name"]
                },

                /// <summary>
                /// Defines the output schema for the tool, including the types and descriptions of output parameters.
                /// </summary>
                OutputSchema = new()
                {
                    /// <summary>
                    /// The data type for the output parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of output parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["tool"] = new()
                        {
                            Type = ["object"],
                            Description = "The tool's metadata including name, description, input and output schemas."
                        }
                    },

                    /// <summary>
                    /// A list of required output parameters that must be included in the tool's response.
                    /// </summary>
                    Required = ["tool"]
                }
            };

            /// <summary>
            /// Represents a system tool that retrieves the full HTML markup of the application's Document Object Model (DOM)
            /// for the current browser session. Useful for inspecting or analyzing the current state of the loaded web page.
            /// </summary>
            //[SystemTool(name: "get_application_dom")]
            public static McpToolModel GetApplicationDom => new()
            {
                /// <summary>
                /// The unique name of the tool, used to identify it within the system.
                /// </summary>
                Name = "get_application_dom",

                /// <summary>
                /// A brief description of what the tool does and its intended use.
                /// </summary>
                Description = "Retrieves the full HTML markup of the application's Document Object Model (DOM) for the current browser session." +
                    "Useful for inspecting or analyzing the current state of the loaded web page.",

                /// <summary>
                /// Defines the input schema for the tool, including the types and descriptions of input parameters.
                /// </summary>
                InputSchema = new()
                {
                    /// <summary>
                    /// The data type for the input parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of input parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["driver_session"] = new()
                        {
                            Type = ["string"],
                            Description = "The unique session ID associated with the current browser session. " +
                                "This ID is used to retrieve the appropriate browser driver for interacting with " +
                                "the session and performing automation tasks."
                        },

                        ["token"] = new()
                        {
                            Type = ["string"],
                            Description = "The G4 Authentication token used to authenticate the session initiation process. " +
                                "This is required to authorize the session creation."
                        }
                    },

                    /// <summary>
                    /// A list of required input parameters that must be provided for the tool to execute successfully.
                    /// </summary>
                    Required = ["driver_session", "token"]
                },

                /// <summary>
                /// Defines the output schema for the tool, including the types and descriptions of output parameters.
                /// </summary>
                OutputSchema = new()
                {
                    /// <summary>
                    /// The data type for the output parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of output parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["driver_session"] = new()
                        {
                            Type = ["string"],
                            Description = "The session key for the browser session from which the DOM was retrieved."
                        },

                        ["value"] = new()
                        {
                            Type = ["string"],
                            Description = "A string containing the full HTML markup of the page’s Document Object Model."
                        }
                    },

                    /// <summary>
                    /// A list of required output parameters that must be included in the tool's response.
                    /// </summary>
                    Required = ["driver_session", "value"]
                }
            };

            // TODO: Consider renaming property to 'GetInstructions' (spelling) to avoid confusion.
            /// <summary>
            /// Represents a system tool that gets instructions for the next tool call.
            /// </summary>
            [SystemTool(name: "get_instructions")]
            public static McpToolModel GetIinstructions => new()
            {
                /// <summary>
                /// Unique tool name used by the agent/runtime to select and invoke this system tool.
                /// </summary>
                Name = "get_instructions",

                /// <summary>
                /// Returns authoritative, versioned policy that governs the *next* tool call.
                /// Must be invoked immediately before any tool call so the agent can merge defaults,
                /// apply guards, and enforce mandatory behaviors.
                /// </summary>
                Description = "Returns authoritative, versioned policy for the next tool call. Must be invoked immediately before any tool call.",

                /// <summary>
                /// Input schema for this tool.
                /// This tool is side-effect free and requires no inputs; it only returns policy.
                /// </summary>
                InputSchema = new()
                {
                    /// <summary>
                    /// Root type of the input payload. No properties are expected.
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// No input properties — the policy is derived from server-side configuration.
                    /// </summary>
                    Properties = [],

                    /// <summary>
                    /// No required inputs — call with an empty object.
                    /// </summary>
                    Required = []
                },

                /// <summary>
                /// Output schema describing the policy object that must be honored by the caller.
                /// </summary>
                OutputSchema = new()
                {
                    /// <summary>
                    /// Root type of the returned policy payload.
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// Policy fields:
                    ///  - policy_version: Identifies the policy document/version in force.
                    ///  - defaults: Baseline arguments to inject into the upcoming tool call.
                    ///  - guards: Validations and preconditions that must pass before calling a tool.
                    ///  - must: Non-negotiable behavioral rules the agent must follow.
                    ///  - ttl_seconds: How long this policy remains valid.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        /// <summary>
                        /// Version metadata for the returned policy.
                        /// Recommended fields (not strictly enforced by schema):
                        ///  - id (string): Stable policy identifier (e.g., "g4/agent/policy").
                        ///  - rev (string): Revision tag or semantic version (e.g., "2025.08.13-rc1").
                        ///  - issued_at (string): ISO-8601 timestamp when this policy was generated.
                        /// The agent should log this for traceability and cache invalidation.
                        /// </summary>
                        ["policy_version"] = new()
                        {
                            Type = ["object"],
                            Description = "Version metadata for the policy (e.g., id, rev, issued_at) for traceability and cache control."
                        },

                        /// <summary>
                        /// Baseline parameters that MUST be merged (caller-supplied values may override where allowed)
                        /// into the very next tool call. Typical keys:
                        ///  - driver (string): Default driver name (e.g., 'ChromeDriver').
                        ///  - driver_binaries (string): Default driver endpoint/path (e.g., 'http://localhost:4444/wd/hub').
                        ///  - token (string): Authentication token to attach to requests.
                        ///  - session (string): Preferred session ID; if missing, a session must be created.
                        ///  - timeouts (object): Default operation timeouts.
                        ///  - retries (object): Default retry policy (max attempts, backoff).
                        /// Use together with 'must' to determine which fields are mandatory vs. overridable.
                        /// </summary>
                        ["defaults"] = new()
                        {
                            Type = ["object"],
                            Description = "Baseline arguments (driver, driver_binaries, token, session, timeouts, retries) to inject into the next tool call."
                        },

                        /// <summary>
                        /// Preconditions and validations that MUST succeed before the next tool call proceeds.
                        /// Examples:
                        ///  - require_fields: ['token'] (ensure security-critical args exist).
                        ///  - allowed_tools: ['start_g4_session','get_tools','find_tool','...'].
                        ///  - session_state: 'existing' | 'new' (enforce session reuse or creation).
                        ///  - token_scope: required scopes/claims.
                        /// If any guard fails, the agent must abort the call and surface a clear error.
                        /// </summary>
                        ["guards"] = new()
                        {
                            Type = ["object"],
                            Description = "Validation rules (required fields, allowed tools, session/token checks). If any fail, the call must be aborted."
                        },

                        /// <summary>
                        /// Non-negotiable instructions the agent MUST follow for the next call.
                        /// Examples:
                        ///  - always include 'token' (fetch from .env if absent; otherwise prompt).
                        ///  - if 'session' is missing, call 'start_g4_session' first using defaults.
                        ///  - call order: get_tools → find_tool → build request → attach_session → call.
                        ///  - log policy_version and chosen tool name for audit.
                        /// These rules supersede caller preferences to ensure safety and correctness.
                        /// </summary>
                        ["must"] = new()
                        {
                            Type = ["object"],
                            Description = "Hard requirements (e.g., include token, ensure/attach driver_session, enforce call order, audit logging)."
                        },

                        /// <summary>
                        /// Time-to-live in seconds for this policy document.
                        /// The agent must re-fetch policy once TTL expires (or sooner if invalidated by server).
                        /// Short TTLs ensure the agent honors rapid config/security changes.
                        /// </summary>
                        ["ttl_seconds"] = new()
                        {
                            Type = ["number"],
                            Description = "Policy lifetime in seconds; agent must re-fetch policy after expiry."
                        }
                    },

                    /// <summary>
                    /// Fields that are guaranteed to be present in a valid response.
                    /// The agent may treat 'policy_version' as optional for forward compatibility.
                    /// </summary>
                    Required = ["defaults", "guards", "must", "ttl_seconds"]
                }
            };

            /// <summary>
            /// Represents a system tool that retrieves an optimal locator for a specific element on a web page.
            /// </summary>
            [SystemTool(name: "get_locator")]
            public static McpToolModel GetLocator => new()
            {
                /// <summary>
                /// Unique tool name used to identify this system tool in the runtime and during tool selection.
                /// </summary>
                Name = "get_locator",

                /// <summary>
                /// Retrieves an optimal locator for a specific element on a web page,
                /// taking into account given constraints, driver session, and intended action.
                /// Locators are chosen to maximize reliability and stability across DOM changes.
                /// </summary>
                Description = "Retrieves a locator for a specific element on the page based on the provided parameters.",

                /// <summary>
                /// Defines the expected input format, including constraints, session details, intent, and authentication.
                /// </summary>
                InputSchema = new()
                {
                    Type = "object",
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["constraints"] = new()
                        {
                            Type = ["object"],
                            Description = "Optional rules influencing how the locator is selected, such as " +
                                "visibility, enablement, and allowed/disallowed strategies.",
                            Properties = new()
                            {
                                ["must_be_visible"] = new()
                                {
                                    Type = ["boolean"],
                                    Description = "If true, the element must be visible on the page to " +
                                        "qualify as a valid locator.",
                                    Default = true
                                },
                                ["must_be_enabled"] = new()
                                {
                                    Type = ["boolean"],
                                    Description = "If true, the element must not be disabled to qualify as a valid locator.",
                                    Default = true
                                },
                                ["prefer"] = new()
                                {
                                    Type = ["array"],
                                    Description = "Preferred locator strategies, in priority order. The first " +
                                        "matching strategy will be used.",
                                    Items = new()
                                    {
                                        Type = "string",
                                        Enum = ["data-testid", "aria", "id", "label", "role", "text", "css", "xpath"]
                                    },
                                },
                                ["forbid"] = new()
                                {
                                    Type = ["array"],
                                    Description = "Locator strategies to avoid. Helps prevent use of " +
                                        "brittle or unstable locators.",
                                    Items = new()
                                    {
                                        Type = "string",
                                        Enum = ["nth-child", "brittle-css"]
                                    }
                                }
                            }
                        },
                        ["driver_session"] = new()
                        {
                            Type = ["string"],
                            Description = "Unique identifier for the active browser session in which the " +
                                "locator search is performed."
                        },
                        ["intent"] = new()
                        {
                            Type = ["string"],
                            Description = "Description of the intended interaction with the element, e.g., " +
                                "'click login button' or 'type into search field'."
                        },
                        ["openai_token"] = new()
                        {
                            Type = ["string"],
                            Description = "OpenAI authentication token for verifying and authorizing the locator retrieval request."
                        },
                        ["openai_uri"] = new()
                        {
                            Type = ["string"],
                            Description = "OpenAI API endpoint URI for the request."
                        },
                        ["token"] = new()
                        {
                            Type = ["string"],
                            Description = "G4 authentication token for verifying and authorizing the locator retrieval request."
                        }
                    },
                    Required = ["driver_session", "openai_token", "openai_uri", "intent", "token"]
                },

                /// <summary>
                /// Defines the shape and meaning of the locator output.
                /// </summary>
                OutputSchema = new()
                {
                    Type = "object",
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["driver_session"] = new()
                        {
                            Type = ["string"],
                            Description = "The browser session identifier associated with this locator result."
                        },
                        ["value"] = new()
                        {
                            Type = ["object"],
                            Description = "Locator details, including primary and fallback strategies, along with " +
                                "human-readable context.",
                            Properties = new()
                            {
                                ["primary_locator"] = new()
                                {
                                    Type = ["object"],
                                    Description = "Main locator used to find the element, optimized for reliability.",
                                    Properties = new()
                                    {
                                        ["value"] = new()
                                        {
                                            Type = ["string"],
                                            Description = "Locator string (e.g., CSS selector or XPath) used in element " +
                                                "identification."
                                        },
                                        ["using"] = new()
                                        {
                                            Type = ["string"],
                                            Description = "Locator strategy type, such as 'css', 'xpath', or 'id'.",
                                            Enum = ["CssSelector", "Xpath", "Id"]
                                        }
                                    }
                                },
                                ["fallback_locator"] = new()
                                {
                                    Type = ["object"],
                                    Description = "Alternative locator to use if the primary locator fails. Useful for " +
                                        "elements with multiple stable selectors.",
                                    Properties = new()
                                    {
                                        ["value"] = new()
                                        {
                                            Type = ["string"],
                                            Description = "Fallback locator string (e.g., CSS selector or XPath)."
                                        },
                                        ["using"] = new()
                                        {
                                            Type = ["string"],
                                            Description = "Strategy type used by the fallback locator.",
                                            Enum = ["CssSelector", "Xpath", "Id"]
                                        }
                                    }
                                },
                                ["description"] = new()
                                {
                                    Type = ["string"],
                                    Description = "Optional human-readable description of the element being located, " +
                                        "for debugging and clarity."
                                }
                            }
                        }
                    },
                    Required = ["driver_session", "value"]
                }
            };

            /// <summary>
            /// Represents a system tool that starts the execution of a G4 rule using the provided parameters.
            /// The rule execution involves interacting with the browser session and processing the rule's logic to produce a result.
            /// </summary>
            [SystemTool(name: "start_g4_rule")]
            public static McpToolModel StartG4Rule => new()
            {
                /// <summary>
                /// The unique name of the tool, used to identify it within the system.
                /// </summary>
                Name = "start_g4_rule",

                /// <summary>
                /// A brief description of what the tool does.
                /// This tool initiates the execution of a G4 rule based on the provided session, authentication token, and rule configuration.
                /// </summary>
                Description = "Starts a G4 rule execution with the provided parameters.",

                /// <summary>
                /// Defines the input schema for the tool, including the types and descriptions of input parameters.
                /// </summary>
                InputSchema = new()
                {
                    /// <summary>
                    /// The data type for the input parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of input parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["driver_session"] = new()
                        {
                            Type = ["string"],
                            Description = "The unique session ID associated with the current browser session. " +
                                "This ID is used to retrieve the appropriate browser driver for interacting with " +
                                "the session and performing automation tasks."
                        },

                        ["token"] = new()
                        {
                            Type = ["string"],
                            Description = "The G4 Authentication token used to authenticate the session initiation process. " +
                                "This is required to authorize the session creation."
                        },

                        ["rule"] = new()
                        {
                            Type = ["object"],
                            Description = "The G4 rule to be executed, including its parameters and configuration."
                        }
                    },

                    /// <summary>
                    /// A list of required input parameters that must be provided for the tool to execute successfully.
                    /// </summary>
                    Required = ["driver_session", "token", "rule"]
                },

                /// <summary>
                /// Defines the output schema for the tool, including the types and descriptions of output parameters.
                /// </summary>
                OutputSchema = new()
                {
                    /// <summary>
                    /// The data type for the output parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of output parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["driver_session"] = new()
                        {
                            Type = ["string"],
                            Description = "The session key for the browser session from which the rule was executed."
                        },

                        ["value"] = new()
                        {
                            Type = ["object"],
                            Description = "The result of the rule execution, including any output or response data."
                        }
                    },

                    /// <summary>
                    /// A list of required output parameters that must be included in the tool's response.
                    /// </summary>
                    Required = ["driver_session", "value"]
                }
            };

            /// <summary>
            /// This tool starts a new G4 session by using specified driver binaries, browser (platform) name, and headless option.
            /// The session is initiated with a given G4 authentication token for secure access.
            /// </summary>
            [SystemTool(name: "start_g4_session")]
            public static McpToolModel StartG4Session => new()
            {
                /// <summary>
                /// The unique name of the tool. Used to identify the tool in the system.
                /// </summary>
                Name = "start_g4_session",

                /// <summary>
                /// A short description of what the tool does. Provides context for its use in automation workflows.
                /// </summary>
                Description = "Starts a new G4 session using specified driver binaries, browser (platform) name, and headless option.",

                /// <summary>
                /// Defines the expected input parameters required by this tool to start the G4 session.
                /// </summary>
                InputSchema = new()
                {
                    /// <summary>
                    /// The data type of the input parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of input parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["driver"] = new()
                        {
                            Type = ["string"],
                            Description = "The name of the browser driver to use (e.g., ChromeDriver, GeckoDriver). " +
                                "This is necessary for the automation to interact with the browser."
                        },

                        ["driver_binaries"] = new()
                        {
                            Type = ["string"],
                            Description = "The path to the browser driver executable (e.g., ChromeDriver) or the URL of the Selenium Grid endpoint. " +
                                "This is necessary for the automation to interact with the browser."
                        },

                        ["token"] = new()
                        {
                            Type = ["string"],
                            Description = "The G4 Authentication token used to authenticate the session initiation process. " +
                                "This is required to authorize the session creation."
                        }
                    },

                    /// <summary>
                    /// A list of required input parameters. These parameters must be provided for the tool to execute successfully.
                    /// </summary>
                    Required = ["driver", "driver_binaries", "token"]
                },

                /// <summary>
                /// Defines the expected output schema after the tool has successfully run.
                /// </summary>
                OutputSchema = new()
                {
                    /// <summary>
                    /// The data type of the input parameters (an object in this case).
                    /// </summary
                    Type = "object",

                    /// <summary>
                    /// A dictionary of output parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["driver_session"] = new()
                        {
                            Type = ["string"],
                            Description = "A unique identifier assigned to the newly created browser session. " +
                                "This identifier can be used for further interaction with the session."
                        }
                    },

                    /// <summary>
                    /// A list of required output parameters. These parameters must be present in the tool's response.
                    /// </summary>
                    Required = ["driver_session"]
                }
            };

            /// <summary>
            /// Represents a system tool that retrieves the full list of available tools that the Copilot agent can invoke.
            /// This tool provides the metadata and schemas for all the tools that are available in the Copilot environment.
            /// </summary>
            [SystemTool(name: "get_tools")]
            public static McpToolModel GetTools => new()
            {
                /// <summary>
                /// The unique name of the tool, used to identify it within the system.
                /// </summary>
                Name = "get_tools",

                /// <summary>
                /// A brief description of what the tool does.
                /// This tool returns the full list of tools that the Copilot agent can invoke, including their metadata and schemas.
                /// </summary>
                Description = "Retrieves the full list of available tools that the Copilot agent can invoke.",

                /// <summary>
                /// Defines the input schema for the tool, including the types and descriptions of input parameters.
                /// </summary>
                InputSchema = new()
                {
                    /// <summary>
                    /// The data type for the input parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// An empty list of properties, as this tool does not require any specific input parameters.
                    /// </summary>
                    Properties = [],

                    /// <summary>
                    /// No required input parameters for this tool.
                    /// </summary>
                    Required = []
                },

                /// <summary>
                /// Defines the output schema for the tool, including the types and descriptions of output parameters.
                /// </summary>
                OutputSchema = new()
                {
                    /// <summary>
                    /// The data type for the output parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of output parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["tools"] = new()
                        {
                            Type = ["array", "object"],
                            Description = "An array of tool objects, each containing name, description, input and output schemas."
                        }
                    },

                    /// <summary>
                    /// A list of required output parameters. "tools" is required as the main result of the tool.
                    /// </summary>
                    Required = ["tools"]
                }
            };
        }
        #endregion

        #region *** Attributes   ***
        /// <summary>
        /// Custom attribute used to mark properties that represent system tools.
        /// This attribute is applied to properties to indicate that they correspond to a system tool, 
        /// which can be used for automation or other tool-based operations.
        /// </summary>
        /// <param name="name">The name of the system tool.</param>
        [AttributeUsage(AttributeTargets.Property)]
        private sealed class SystemToolAttribute(string name) : Attribute
        {
            /// <summary>
            /// Gets the name of the system tool.
            /// </summary>
            public string Name { get; } = name;
        }
        #endregion
    }
}
