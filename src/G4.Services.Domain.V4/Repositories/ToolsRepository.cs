﻿using G4.Api;
using G4.Cache;
using G4.Extensions;
using G4.Models;
using G4.Models.Schema;

using HtmlAgilityPack;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace G4.Services.Domain.V4.Repositories
{
    public class ToolsRepository(IHttpClientFactory clientFactory, CacheManager cache, G4Client client) : IToolsRepository
    {
        #region *** Fields       ***
        // Tracks active browser or agent sessions by session ID.
        private static readonly ConcurrentDictionary<object, object> s_sessions = [];

        // Static, atomically swappable snapshot
        private static ConcurrentDictionary<string, McpToolModel> s_tools =
            FormatTools(cache: CacheManager.Instance);

        private readonly CacheManager _cache = cache;
        private readonly G4Client _client = client;

        // HTTP client configured for OpenAI API interactions, using a named configuration.
        private readonly HttpClient _httpClient = clientFactory.CreateClient(name: "openai");
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public McpToolModel FindTool(string intent, string toolName)
        {
            // Look up the tool by name in the internal registry.
            return s_tools.GetValueOrDefault(toolName);
        }

        /// <inheritdoc />
        public IDictionary<string, object> GetDocumentModel(string driverSession, string token)
        {
            // Prepare the invocation options that contain all the required context
            // for retrieving the DOM from the G4 engine.
            var options = new InvokeOptions
            {
                DriverSession = driverSession, // Session identifier to fetch the DOM for
                Token = token,                 // Token authorizing the G4 engine to perform DOM retrieval
                G4Client = _client,            // Reference to the G4 engine client instance
                HttpClient = _httpClient,      // HTTP client used for underlying communication
                Sessions = s_sessions,         // Active sessions collection used by the engine
                Tools = s_tools                // Registered tools available in the current context
            };

            // Delegate the actual DOM retrieval to the helper method,
            // which uses the constructed options.
            return GetApplicationDom(options);
        }

        /// <inheritdoc />
        public object GetInstructions(string policy)
        {
            // If no policy name is provided, fall back to the "default" policy.
            policy = string.IsNullOrEmpty(policy) ? "default" : policy;

            // Delegate to the shadow instructions provider to retrieve
            // the actual instruction set for the resolved policy name.
            return GetShadowInstructions(policy);
        }

        /// <inheritdoc />
        public IDictionary<string, McpToolModel> GetTools(params string[] types)
        {
            // Filter the tools based on the specified types.
            return types.Length switch
            {
                // If no types are specified, return all tools from the registry.
                0 => s_tools,
                // If specific types are requested, filter the tools by those types.
                _ => new ConcurrentDictionary<string, McpToolModel>(
                    s_tools.Where(tool => types.Contains(tool.Value.Type, StringComparer.OrdinalIgnoreCase)),
                    StringComparer.OrdinalIgnoreCase)
            };
        }

        // TODO: Get policy from the parameters and return different instructions based on policy.
        /// <inheritdoc />
        public object InvokeTool(JsonElement parameters)
        {
            // Extract the "arguments" object from the JSON parameters.
            // This contains tool-specific input such as driver settings, session IDs, etc.
            var options = new InvokeOptions(parameters)
            {
                G4Client = _client,
                HttpClient = _httpClient,
                Sessions = s_sessions,
                Tools = s_tools
            };

            options.Rule = ConvertToRule(options.Arguments, s_tools);

            // Look up the tool definition in the registered tools dictionary.
            // If the tool is not found, 'tool' will be null and handled in the default branch below.
            var tool = s_tools.GetValueOrDefault(options.ToolName);

            // Match the tool by name and execute the corresponding handler.
            // Some tools are built-in system tools, others are dynamically loaded plugins.
            return tool switch
            {
                // Built-in: Finds and returns metadata about a tool by its name.
                { Name: "find_tool" } => FindTool(options),

                // Built-in: Retrieves the current application's DOM (Document Object Model).
                { Name: "get_application_dom" } => GetApplicationDom(options),

                // Built-in: Returns the instructions for the next tool call, including policies and defaults.
                { Name: "get_instructions" } => GetInstructions(policy: string.Empty),

                // Built-in: Retrieves the locator for a specific element on the page.
                { Name: "get_locator" } => ResolveLocator(options),

                // Built-in: Lists all available tools.
                { Name: "get_tools" } => s_tools.Values,

                // Built-in: Starts a new G4 browser automation session.
                { Name: "start_g4_session" } => StartG4Session(options),

                // Default: Assumes this is a plugin-based tool and converts parameters into an executable rule.
                _ => StartG4Rule(options)
            };
        }

        /// <inheritdoc />
        public string ResolveLocator(ResolveLocatorInputSchema schema)
        {
            // Prepare the invocation options that contain all the required context
            // for retrieving the DOM from the G4 engine.
            var options = new InvokeOptions
            {
                DriverSession = schema.DriverSession, // Session identifier to fetch the DOM for
                G4Client = _client,                   // Reference to the G4 engine client instance
                HttpClient = _httpClient,             // HTTP client used for underlying communication
                Intent = schema.Intent,               // The intent describing the element to locate
                OpenaiApiKey = schema.OpenaiApiKey,   // OpenAI API key for authentication
                OpenaiModel = schema.OpenaiModel,     // OpenAI model to use for locator resolution
                OpenaiUri = schema.OpenaiUri,         // OpenAI API endpoint URI
                Sessions = s_sessions,                // Active sessions collection used by the engine
                Token = schema.Token,                 // Token authorizing the G4 engine to perform DOM retrieval
                Tools = s_tools                       // Registered tools available in the current context
            };

            // Return the resolved locator using the provided intent and the retrieved DOM.
            return ResolveLocator(options);
        }

        /// <inheritdoc />
        public void SyncTools()
        {
            // Rebuild the tools collection from the cache manager.
            var rebuilt = new ConcurrentDictionary<string, McpToolModel>(FormatTools(_cache));

            // Atomically replace the current tools collection with the rebuilt one.
            Interlocked.Exchange(ref s_tools, rebuilt);
        }

        // Finds and returns a tool model from the available tool collection
        // using the tool name provided in the <see cref="InvokeOptions"/> arguments.
        private static McpToolModel FindTool(InvokeOptions options)
        {
            // Extract the "tool_name" argument from the provided options.
            // If no argument exists, default to an empty string.
            var toolName = options.Arguments.GetOrDefault("tool_name", () => string.Empty);

            // Check if the tool name is non-empty. If so, attempt to retrieve
            // the corresponding tool from the Tools dictionary.
            // If the tool name is empty or not found in the collection, return null.
            return !string.IsNullOrEmpty(toolName)
                ? options.Tools.GetValueOrDefault(toolName)
                : null;
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
                .DistinctBy(manifest => manifest.Key);

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

        // Retrieves and cleans the DOM of a web page through the automation process by invoking a JavaScript script
        // to extract the HTML content and then processing it to remove unwanted elements.
        private static Dictionary<string, object> GetApplicationDom(InvokeOptions options)
        {
            // JavaScript to get the entire HTML content of the document.
            var script = "return document.body.outerHTML;";

            // Create the action rule to register the document object output as a session parameter.
            var registerParameterRule = new ActionRuleModel
            {
                Argument = "{{$ " +
                    "--Name:DocumentObjectOutput " +
                    "--Value:{{$New-ScriptResult --Src:" + script + "}} " +
                    "--Scope:Session}}",
                PluginName = "RegisterParameter"
            };

            // Create the automation model and invoke it using the provided client.
            var automation = NewAutomation(options.DriverSession, options.Token, registerParameterRule);
            var response = options.G4Client.Automation.Invoke(automation);

            // Extract the session information from the response.
            var session = response.Values.Last().Sessions.Last();

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
        private static object GetShadowInstructions(string policyName)
        {
            return new
            {
                PolicyVersion = "2025.08.13.1",
                Name = policyName,
                CheckList = new[]
                {
                    "Read G4_API_KEY from .env (ask if missing)",
                    "Read OPENAI_API_KEY from .env (ask if missing)",
                    "Read OPENAI_URI from .env (ask if missing)",
                    "Read OPENAI_MODEL from .env (ask if missing)",
                    "Ensure driver_session (start_g4_session if missing with WEB_DRIVER / WEB_DRIVER_REPOSITORY)",
                    "Ensure openai_api_key, openai_uri and openai_model are available for get_locator. If not available, use get_application_dom tool, and build the locator",
                    "Run get_tools to confirm <toolName> exists",
                    "Run find_tool(<toolName>) and validate schema"
                },
                Defaults = new Dictionary<string, object>()
                {
                    ["get_locator.openai_api_key"] = "ENV:OPENAI_API_KEY",
                    ["get_locator.openai_uri"] = "ENV:OPENAI_URI",
                    ["get_locator.openai_model"] = "ENV:OPENAI_MODEL",
                    ["start_g4_session.driver"] = "ENV:WEB_DRIVER",
                    ["start_g4_session.driver_binaries"] = "ENV:WEB_DRIVER_REPOSITORY",
                },
                Guards = new Dictionary<string, object>()
                {
                    ["no_guessing"] = true,
                    ["retry_find_tool_once"] = true,
                    ["on_locator_failure"] = "ask_user"
                },
                PageTool = new Dictionary<string, object>()
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

        // TODO: Decouple from GetApplicationDom and make it a standalone tool.
        // Retrieves a locator for a specific element on the page by sending the DOM and intent
        // to the OpenAI completion API.
        private static string ResolveLocator(InvokeOptions options)
        {
            // Retrieve the current application DOM (document object model)
            // for the given driver session.
            var documentObject = GetApplicationDom(options);

            // Deserialize the intent JSON into a dictionary for modification.
            var inputObject = JsonSerializer.Deserialize<Dictionary<string, object>>(
                json: options.Arguments.GetRawText(),
                options: ICopilotRepository.JsonOptions);

            // Inject the DOM into the intent so the model has page context.
            var intentObject = new Dictionary<string, object>
            {
                ["intent"] = inputObject.GetValueOrDefault(key: "intent", defaultValue: string.Empty),
                ["dom"] = documentObject.GetValueOrDefault("value", "<html></html>"),
            };

            // Serialize the updated intent object into JSON string for request payload.
            var userContent = JsonSerializer.Serialize(
                value: intentObject,
                options: ICopilotRepository.JsonOptions);

            // Build the request payload for OpenAI's ChatCompletion API.
            var openAiRequest = new
            {
                Model = options.OpenaiModel,
                Messages = new[]
                {
                    new
                    {
                        Role = "system",
                        Content = InvokeOptions.SystemPrompts.Get(key: "LocatorsSystemPrompt.md", defaultValue: "")
                    },
                    new
                    {
                        Role = "user",
                        Content = userContent
                    }
                }
            };

            // Serialize the OpenAI request payload into JSON.
            var value = JsonSerializer.Serialize(
                value: openAiRequest,
                options: ICopilotRepository.JsonOptions);

            // Construct the HTTP POST request to OpenAI API.
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(options.OpenaiUri),
                Content = new StringContent(value, Encoding.UTF8, MediaTypeNames.Application.Json)
            };

            // Add the Bearer token for OpenAI authentication.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.OpenaiApiKey);

            // Send the request synchronously and capture the response.
            var response = options.HttpClient.Send(request);

            // Ensure the response indicates success (throws if not 2xx).
            response.EnsureSuccessStatusCode();

            // Read and return the response body as a string (raw JSON).
            return response.Content.ReadAsStringAsync().Result;
        }

        // Starts the automation rule execution by invoking the specified rule on the client, 
        // and retrieves the response, including the driver session and the value of the executed rule.
        private static object StartG4Rule(InvokeOptions options)
        {
            // Create a new automation model using the provided driver binaries, authentication token, and rule.
            var automation = NewAutomation(options.DriverSession, options.Token, options.Rule);

            // Invoke the automation process using the client and retrieve the response.
            var response = options.G4Client.Automation.Invoke(automation);

            // Extract the session object from the response, ensuring we track the latest session.
            var session = response.Values.Last().Sessions.Last();

            // Add the session to the sessions dictionary to keep track of the active session.
            options.Sessions[session.Key] = session.Value;

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
        private static object StartG4Session(InvokeOptions options)
        {
            // Define the driver parameters, including the driver name and the path to the binaries.
            // The driver name (e.g., Chrome, Firefox).
            // The path to the driver binaries or the Selenium Grid URL.
            var driverParameters = new Dictionary<string, object>
            {
                ["driver"] = options.Driver,
                ["driverBinaries"] = options.DriverBinaries
            };

            // Create the automation model which includes the authentication and driver parameters.
            var automation = NewAutomation(driverSession: default, options.Token);

            // Set the driver parameters for the automation model.
            // This dictionary contains configuration for the browser driver (e.g., driver name and path).
            // These parameters are required for the automation process to know which browser and driver binaries to use.
            automation.DriverParameters = driverParameters;

            // Invoke the automation process and get the response.
            var response = options.G4Client.Automation.Invoke(automation);

            // Retrieve the session information from the response.
            var session = response.Values.Last().Sessions.Last();

            // Add the session to the sessions dictionary using the session key.
            options.Sessions[session.Key] = session.Value;

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
        private static ActionRuleModel ConvertToRule(JsonElement arguments, ConcurrentDictionary<string, McpToolModel> tools)
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
                var json = JsonSerializer.Serialize(value: parametersObject, options: ICopilotRepository.G4JsonOptions);
                var parametersCollection = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    json,
                    options: ICopilotRepository.G4JsonOptions)!;

                // Convert keys to PascalCase and render as --Key[:Value] (omit :Value when empty).
                var parametersExpression = parametersCollection
                    .Select(i =>
                        $"--{i.Key.ConvertToPascalCase()}" +
                        (string.IsNullOrEmpty(i.Value) ? "" : $":{i.Value}"))
                    .ToArray();

                // Wrap with G4 template delimiters.
                return "{{$ " + string.Join(" ", parametersExpression) + "}}";
            }

            // Try to extract the rule data from the arguments JSON.
            var ruleData = arguments.TryGetProperty("rule", out var ruleOut)
                ? ruleOut
                : JsonDocument.Parse("{}").RootElement;

            // Retrieve the "name" of the tool to be invoked. Defaults to null if not provided.
            var toolName = ruleData.TryGetProperty("tool_name", out var toolNameOut)
                ? toolNameOut.GetString()
                : string.Empty;

            // Retrieve the plugin name associated with the provided tool name from the internal tool registry (_tools)
            var pluginName = tools.GetValueOrDefault(key: toolName)?.G4Name ?? string.Empty;

            // Get the parameters from the ruleData, defaulting to an empty JSON object if not provided.
            var parameters = ruleData.TryGetProperty("parameters", out var parametersOut)
                ? parametersOut
                : JsonDocument.Parse("{}").RootElement;

            // Format the parameters into a command-line style string (e.g., "--param1:value1 --param2:value2").
            var parametersCli = FormatParameters(parameters);

            // Get the properties from the ruleData, defaulting to a JSON object with the plugin name if not provided.
            var properties = ruleData.TryGetProperty("properties", out var propertiesOut)
                ? propertiesOut.GetRawText()
                : "{\"tool_name\":\"" + pluginName + "\"}";

            // Deserialize the JSON string into the G4RuleModelBase object using the provided JSON options (JsonOptions)
            var rule = JsonSerializer.Deserialize<ActionRuleModel>(
                json: properties,
                options: ICopilotRepository.JsonOptions);

            // Set the PluginName property of the rule to the retrieved plugin name
            rule.PluginName = pluginName;

            // Set the Argument property of the rule to the formatted parameters if provided
            rule.Argument = string.IsNullOrEmpty(parametersCli) || parametersCli.Equals("{{$ }}")
                ? rule.Argument
                : parametersCli;

            // Return the created rule
            return rule;
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
            public static McpToolModel FindToolTool => new()
            {
                /// <summary>
                /// The unique name of the tool, used to identify it within the system.
                /// </summary>
                Name = "find_tool",

                /// <summary>
                /// A brief description of what the tool does.
                /// This tool retrieves the metadata and schema for a specific tool identified by its unique name.
                /// </summary>
                Description = "Retrieves the metadata and schema for a tool. " +
                    "Uses the tool name if available, otherwise falls back to intent matching to find the best match.",

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
                        ["intent"] = new()
                        {
                            Type = ["string"],
                            Description = "The intent or purpose for which the tool is being sought."
                        },
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
            [SystemTool(name: "get_application_dom")]
            public static McpToolModel GetApplicationDomTool => new()
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

            /// <summary>
            /// Represents a system tool that gets instructions for the next tool call.
            /// </summary>
            [SystemTool(name: "get_instructions")]
            public static McpToolModel GetInstructionsTool => new()
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
            public static McpToolModel GetLocatorTool => new()
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
                        ["openai_api_key"] = new()
                        {
                            Type = ["string"],
                            Description = "OpenAI authentication token for verifying and authorizing the locator retrieval request."
                        },
                        ["openai_model"] = new()
                        {
                            Type = ["string"],
                            Description = "Specifies the OpenAI model identifier (e.g., 'gpt-4o', 'gpt-4.1-mini')."
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
                    Required = ["driver_session", "openai_model", "openai_token", "openai_uri", "intent", "token"]
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
            public static McpToolModel StartG4RuleTool => new()
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
            public static McpToolModel StartG4SessionTool => new()
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
            public static McpToolModel GetToolsTool => new()
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

        /// <summary>
        /// Encapsulates the options required to invoke a tool,
        /// including driver configuration, session management,
        /// authentication, and OpenAI integration settings.
        /// </summary>
        private sealed class InvokeOptions
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="InvokeOptions"/> class.
            /// Provides an empty options object where properties may be set manually.
            /// </summary>
            public InvokeOptions()
            { }

            /// <summary>
            /// Initializes a new instance of the <see cref="InvokeOptions"/> class
            /// using a <see cref="JsonElement"/> containing tool invocation parameters.
            /// </summary>
            /// <param name="parameters">The JSON parameters containing an <c>"arguments"</c> object with tool-specific settings.</param>
            public InvokeOptions(JsonElement parameters)
            {
                // Extract the "arguments" object from the JSON parameters.
                // This contains tool-specific input such as driver settings, session IDs, etc.
                var arguments = parameters.GetProperty("arguments");
                Arguments = arguments;

                // Retrieve the "driver" argument (e.g., ChromeDriver).
                // Defaults to null if not provided.
                Driver = arguments.GetOrDefault("driver", () => default(string));

                // Retrieve the "driver_binaries" argument (path or URL to driver binaries).
                // Defaults to null if not provided.
                DriverBinaries = arguments.GetOrDefault("driver_binaries", () => default(string));

                // Retrieve the "driver_session" argument (existing session ID for reusing a browser session).
                // Defaults to null if not provided.
                DriverSession = arguments.GetOrDefault("driver_session", () => default(string));

                // Retrieve the OpenAI API key for authentication with the OpenAI API.
                // Defaults to null if not provided.
                OpenaiApiKey = arguments.GetOrDefault("openai_api_key", () => default(string));

                // Retrieve the OpenAI model identifier (e.g., gpt-4, gpt-5).
                // Defaults to null if not provided.
                OpenaiModel = arguments.GetOrDefault("openai_model", () => default(string));

                // Retrieve the OpenAI API base URI (custom endpoint if applicable).
                // Defaults to null if not provided.
                OpenaiUri = arguments.GetOrDefault("openai_uri", () => default(string));

                // Retrieve the "token" argument (general-purpose authentication or API token).
                // Defaults to null if not provided.
                Token = arguments.GetOrDefault("token", () => default(string));

                // Retrieve the "name" argument (the tool identifier to be invoked).
                // Defaults to null if not provided.
                ToolName = parameters.GetOrDefault("name", () => default(string));
            }

            public static ReadOnlyDictionary<string, string> SystemPrompts => new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LocatorsSystemPrompt.md"] = ReadSystemPrompt(instrcutionsManifest: "LocatorsSystemPrompt.md")
            });

            /// <summary>
            /// The raw JSON <c>"arguments"</c> object supplied to the tool.
            /// </summary>
            public JsonElement Arguments { get; set; }

            /// <summary>
            /// The name or type of driver to use (e.g., "ChromeDriver").
            /// </summary>
            public string Driver { get; set; }

            /// <summary>
            /// The path or URL to driver binaries required by the tool.
            /// </summary>
            public string DriverBinaries { get; set; }

            /// <summary>
            /// An existing driver session ID, allowing reuse of an open session.
            /// </summary>
            public string DriverSession { get; set; }

            /// <summary>
            /// A reference to the G4 client instance used to interact with the engine.
            /// </summary>
            public G4Client G4Client { get; set; }

            /// <summary>
            /// The HTTP client used for making network requests during tool invocation.
            /// </summary>
            public HttpClient HttpClient { get; set; }

            /// <summary>
            /// The intent that describes the purpose of the invocation,
            /// which can be used for semantic or vector-based lookup of tools.
            /// </summary>
            public string Intent { get; set; }

            /// <summary>
            /// The OpenAI API key used for authentication.
            /// </summary>
            public string OpenaiApiKey { get; set; }

            /// <summary>
            /// The OpenAI model identifier (e.g., gpt-4, gpt-5).
            /// </summary>
            public string OpenaiModel { get; set; }

            /// <summary>
            /// The OpenAI API base URI (default or custom endpoint).
            /// </summary>
            public string OpenaiUri { get; set; }

            /// <summary>
            /// A reference to the associated rule definition for this invocation.
            /// </summary>
            public G4RuleModelBase Rule { get; set; }

            /// <summary>
            /// A thread-safe collection of active sessions maintained by the engine.
            /// </summary>
            public ConcurrentDictionary<object, object> Sessions { get; set; }

            /// <summary>
            /// The general-purpose authentication token (if provided).
            /// </summary>
            public string Token { get; set; }

            /// <summary>
            /// The collection of registered tools available in the current context.
            /// </summary>
            public ConcurrentDictionary<string, McpToolModel> Tools { get; set; }

            /// <summary>
            /// The name of the tool being invoked.
            /// </summary>
            public string ToolName { get; set; }

            // Reads the contents of an embedded resource file (system prompt) from the assembly.
            private static string ReadSystemPrompt(string instrcutionsManifest)
            {
                // Get a reference to the executing assembly.
                var assembly = Assembly.GetExecutingAssembly();

                // Retrieve the list of all manifest resource names embedded in the assembly.
                var manifests = assembly.GetManifestResourceNames();

                // Find the first manifest that ends with the provided name (case-insensitive).
                var name = manifests.FirstOrDefault(
                    i => i.EndsWith(instrcutionsManifest, StringComparison.OrdinalIgnoreCase));

                try
                {
                    // Open a stream for the located embedded resource.
                    var stream = Assembly
                        .GetExecutingAssembly()
                        .GetManifestResourceStream(name);

                    // If no resource was found, return empty string safely.
                    if (stream == null)
                        return string.Empty;

                    // Read the resource stream fully as UTF-8 text.
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    return reader.ReadToEnd();
                }
                catch
                {
                    // In case of any error (e.g., resource not found, IO issues), return empty.
                    return string.Empty;
                }
            }
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
