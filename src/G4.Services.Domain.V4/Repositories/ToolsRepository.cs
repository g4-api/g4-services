using G4.Abstraction.Cli;
using G4.Api;
using G4.Cache;
using G4.Extensions;
using G4.Models;
using G4.Models.Schema;
using G4.Services.Domain.V4.Models;
using G4.Services.Domain.V4.Models.Schema;
using G4.Settings;
using G4.WebDriver.Exceptions;

using HtmlAgilityPack;

using ModelContextProtocol.Protocol;

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
    /// <summary>
    /// Provides access to available MCP and G4 tool definitions, including
    /// retrieval, lookup, and execution support through the underlying services.
    /// </summary>
    /// <remarks>
    /// This repository coordinates tool discovery and invocation by using the
    /// shared HTTP client factory, cache manager, and G4 client dependencies.
    /// It acts as the main entry point for working with the registered tool catalog.
    /// </remarks>
    public class ToolsRepository(
        IHttpClientFactory clientFactory,
        CacheManager cache,
        G4Client client) : IToolsRepository
    {
        #region *** Fields       ***
        // Buffer for storing intermediate results or state
        // related to G4 rules, keyed by a string identifier.
        private static readonly ConcurrentDictionary<string, ConcurrentBag<(long Timestamp, G4RuleModelBase Rule)>> s_buffer = [];

        // Factory for converting CLI-style arguments into structured data.
        private static readonly CliFactory s_cliFactory = new();

        // Tracks active browser or agent sessions by session ID.
        private static readonly ConcurrentDictionary<object, object> s_sessions = [];

        // Static, atomically swappable snapshot
        private static ConcurrentDictionary<string, McpToolModel> s_tools =
            FormatTools(cache: CacheManager.Instance);

        // HTTP client configured for OpenAI API interactions, using a named configuration.
        private readonly HttpClient _httpClient = clientFactory.CreateClient(name: "openai");

        // Lexical retrieval manager instance used for finding relevant tools based on intent.
        private readonly LexicalRetrievalManager _retrievalManager = new(CacheManager.Instance);
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public object CallTool(JsonElement parameters)
        {
            // Extract the "arguments" object from the JSON parameters.
            // This contains tool-specific input such as driver settings, session IDs, etc.
            var options = new InvokeOptions(parameters)
            {
                Buffer = s_buffer,
                G4Client = client,
                HttpClient = _httpClient,
                Sessions = s_sessions,
                Tools = s_tools
            };

            // Extract and convert the "arguments" to an executable rule format.
            options.Rule = ConvertToRule(options.Arguments, options.Intent, s_tools);

            // Look up the tool definition in the registered tools dictionary.
            // If the tool is not found, 'tool' will be null and handled in the default branch below.
            var tool = s_tools.GetValueOrDefault(options.ToolName);

            // Read the friendly intent text used to retrieve relevant tools.
            // Default to an empty string when the argument is missing.
            var intent = options.Intent?.AgentIntent ?? string.Empty;

            // Match the tool by name and execute the corresponding handler.
            // Some tools are built-in system tools, others are dynamically loaded plugins.
            return tool switch
            {
                // Built-in: Converts input parameters into executable rules.
                { Name: "g4.ConvertToRule" } => new { options.Rule },

                // Built-in: Finds and returns relevant examples based on the provided intent and tool filters.
                { Name: "g4.FindExamples" } => FindExamples(options.Arguments),

                // Built-in: Finds and returns metadata about a tool by its name.
                { Name: "g4.FindTool" } => FindTool(options),

                // Built-in: Lists all available tools.
                { Name: "g4.FindTools" } => _retrievalManager.FindTools(intent),

                // Built-in: Retrieves the current application's DOM (Document Object Model).
                { Name: "g4.GetApplicationDom" } => GetApplicationDom(options),

                // Built-in: Retrieves the current set of rules stored in the buffer for a given key.
                { Name: "g4.GetBuffer" } => GetBuffer(options),

                // Built-in: Clears the rules stored in the buffer for a given key.
                { Name: "g4.RemoveBuffer" } => RemoveBuffer(options),

                // Built-in: Removes an active session from the sessions registry.
                { Name: "g4.RemoveSession" } => RemoveSession(options),

                // Built-in: Retrieves the locator for a specific element on the page.
                { Name: "g4.ResolveLocator" } => ResolveLocator(options),

                // Built-in: Starts a new G4 browser automation session.
                { Name: "g4.StartSession" } => StartSession(options),

                // Default: Assumes this is a plugin-based tool and converts parameters into an executable rule.
                _ => SendRule(options)
            };

            // Finds and returns a tool model from the available tool collection
            // using the tool name provided in the InvokeOptions arguments.
            static object FindTool(InvokeOptions options)
            {
                // Read the "tool_name" argument from the invocation payload.
                // When it is missing, default to an empty string.
                var toolName = options.Arguments.GetOrDefault("tool_name", () => string.Empty);

                // Fall back to "toolName" when "tool_name" is not present.
                toolName = string.IsNullOrEmpty(toolName)
                    ? options.Arguments.GetOrDefault("toolName", () => string.Empty)
                    : toolName;

                // Try to resolve the tool directly from the available tools dictionary.
                var isName = options.Tools.TryGetValue(key: toolName, out var tool);

                // If the direct lookup failed, try matching by the tool's G4 name instead.
                tool = isName
                    ? tool
                    : options.Tools.Values.FirstOrDefault(i => i.QualifiedName.Equals(toolName, StringComparison.OrdinalIgnoreCase));

                // Return the matched tool when a tool name was provided.
                // Return null when the caller did not supply any tool name.
                return !string.IsNullOrEmpty(toolName)
                    ? new { Tool = tool.ClientTool }
                    : null;
            }
        }

        /// <inheritdoc />
        public object FindExamples(JsonElement parameters)
        {
            // Read the raw JSON payload from the input parameters.
            var json = parameters.GetRawText();

            // Reuse the shared application JSON serializer options.
            var options = AppSettings.JsonOptions;

            // Deserialize the JSON payload into the strongly typed example query model.
            var query = JsonSerializer.Deserialize<ExamplesQueryModel>(json, options);

            // Delegate the actual example lookup to the typed overload.
            return FindExamples(query);
        }

        /// <inheritdoc />
        public object FindExamples(ExamplesQueryModel query)
        {
            // Search the retrieval manager for examples that match the requested tool filters and intent.
            var examples = _retrievalManager
                .FindExamples(query.ToolName, query.Namespace, query.Intent.AgentIntent, take: query.MaxResults)
                .Examples
                .Select(i => (i.Example.Example, i.Score));

            // Exclude the plugin name because it is already implied by the example rule itself.
            var exclude = new[] { nameof(G4RuleModelBase.PluginName) };

            // Project each matched example into the response model with exported properties,
            // parsed CLI parameters, the original rule, and the calculated score.
            var result = examples.Select(i => new ExamplesResultModel()
            {
                ToolProperties = i.Example.Rule.ExportProperties(exclude),
                ToolParameters = s_cliFactory
                    .ConvertToDictionary(
                        cli: i.Example?.Rule?.Argument ?? string.Empty,
                        normalize: false)
                    .ToDictionary(StringComparer.OrdinalIgnoreCase),
                Rule = i.Example.Rule,
                Score = i.Score
            });

            // Return the projected results to the caller.
            return new
            {
                Examples = result
            };
        }

        /// <inheritdoc />
        public McpToolModel FindTool(string intent, string toolName)
        {
            // Look up the tool by name in the internal registry.
            return s_tools.GetValueOrDefault(toolName);
        }

        /// <inheritdoc />
        public IDictionary<string, McpToolModel> FindTools(JsonElement parameters)
        {
            // Use case-insensitive matching for tool names and type comparisons.
            var comparer = StringComparer.OrdinalIgnoreCase;

            // Wrap the raw JSON payload so the arguments object can be accessed
            // through a consistent model.
            var options = new InvokeOptions(parameters);

            // Read the requested tool types from the arguments payload.
            // Ignore null, empty, or whitespace entries and default to an empty array when missing.
            var types = options.Arguments.TryGetProperty("types", out var typesProperty) &&
                    typesProperty.ValueKind == JsonValueKind.Array
                ? typesProperty.EnumerateArray()
                    .Select(i => i.GetString())
                    .OfType<string>()
                    .Where(i => !string.IsNullOrWhiteSpace(i))
                    .ToArray()
                : [];

            // Read the optional maximum number of results to return.
            // Default to 3 when the value is missing or invalid.
            var take = options.Arguments.TryGetProperty("take", out var takeProperty) &&
                    takeProperty.ValueKind == JsonValueKind.Number &&
                    takeProperty.TryGetInt32(out var takeValue)
                ? takeValue
                : 3;

            // When the caller is not explicitly asking for system tools, use lexical retrieval
            // to find the most relevant tools for the provided intent.
            if (!types.Any(i => i.Equals("system-tool", StringComparison.OrdinalIgnoreCase)))
            {
                return new LexicalRetrievalManager(cache)
                    .FindTools(prompt: options.Intent?.AgentIntent ?? string.Empty, take: take)
                    .Tools
                    .Select(result => s_tools.GetValueOrDefault(result.Name))
                    .Where(tool => tool != null)
                    .ToDictionary(tool => tool.Name, tool => tool, comparer);
            }

            // Otherwise, return all tools whose type matches one of the requested types.
            var toolsResult = new ConcurrentDictionary<string, McpToolModel>(comparer);

            // Iterate through the entire tool collection
            // and filter by the requested types.
            foreach (var tool in s_tools)
            {
                // Check whether the current tool type is included in the requested type list.
                var isTypes = types.Contains(tool.Value.Type, comparer);

                // Add the tool to the result only when its type matches.
                if (isTypes)
                {
                    toolsResult[tool.Key] = tool.Value;
                }
            }

            // Return the filtered tool set.
            return toolsResult;
        }

        /// <inheritdoc />
        public IDictionary<string, McpToolModel> FindTools(string prompt)
        {
            // Delegate to the main overload with default maxResult=3 and threshold=0
            return FindTools(prompt, maxResult: 3, threshold: 0);
        }

        /// <inheritdoc />
        public IDictionary<string, McpToolModel> FindTools(string prompt, int maxResult)
        {
            // Delegate to the main overload with threshold=0
            return FindTools(prompt, maxResult, threshold: 0);
        }

        /// <inheritdoc />
        public IDictionary<string, McpToolModel> FindTools(string intent, int maxResult, int threshold)
        {
            // Validate and sanitize input parameters,
            // ensuring maxResult is positive and threshold is non-negative.
            maxResult = maxResult <= 0 ? 3 : maxResult;
            threshold = threshold < 0 ? 0 : threshold;

            // Initialize the lexical retrieval manager with the current plugin cache
            var retrievalManager = new LexicalRetrievalManager(cache);

            // Retrieve the most relevant tool names based on lexical matching
            var results = retrievalManager
                .FindTools(prompt: intent, take: maxResult)
                .Tools
                .Where(i => i.Score >= threshold);

            // Map the tool names to their corresponding McpToolModel instances
            return results
                .Select(i => s_tools.GetValueOrDefault(i.Name))
                .Where(i => i != null)
                .ToDictionary(
                    i => i.Name,
                    i => i,
                    StringComparer.OrdinalIgnoreCase);
        }

        // TODO: Needs to refacor to take profile and segmentation options into account.
        /// <inheritdoc />
        public IDictionary<string, object> GetDocumentModel(string driverSession, string token)
        {
            // Prepare the invocation options that contain all the required context
            // for retrieving the DOM from the G4 engine.
            var options = new InvokeOptions
            {
                DriverSession = driverSession, // Session identifier to fetch the DOM for
                Token = token,                 // Token authorizing the G4 engine to perform DOM retrieval
                G4Client = client,             // Reference to the G4 engine client instance
                HttpClient = _httpClient,      // HTTP client used for underlying communication
                Sessions = s_sessions,         // Active sessions collection used by the engine
                Tools = s_tools                // Registered tools available in the current context
            };

            // Delegate the actual DOM retrieval to the helper method,
            // which uses the constructed options.
            return GetApplicationDom(options);
        }

        /// <inheritdoc />
        public object ResolveLocator(ResolveLocatorInputSchema schema)
        {
            // Prepare the invocation options that contain all the required context
            // for retrieving the DOM from the G4 engine.
            var options = new InvokeOptions
            {
                DriverSession = schema.DriverSession, // Session identifier to fetch the DOM for
                G4Client = client,                    // Reference to the G4 engine client instance
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
        public object StartSession(StartSessionInputSchema schema)
        {
            // Build the invocation options using the required driver information
            // provided in the input schema.
            var options = new InvokeOptions
            {
                Driver = schema.Driver,
                DriverBinaries = schema.DriverBinaries,
                G4Client = client,
                HttpClient = _httpClient,
                Sessions = s_sessions,
                Token = schema.Token,
                Tools = s_tools
            };

            // Delegate session creation to the G4 engine.
            return StartSession(options);
        }

        /// <inheritdoc />
        public object SendRule(SendRuleInputSchema schema)
        {
            // Wrap the incoming rule in an anonymous arguments object so it can be
            // serialized and converted through the standard rule conversion pipeline.
            var arguments = new
            {
                schema.Rule
            };

            // Serialize the arguments payload using the shared JSON settings.
            var json = JsonSerializer.Serialize(value: arguments, AppSettings.JsonOptions);

            // Parse the serialized payload into a JsonElement so it can be passed
            // into the rule conversion routine.
            var jsonElement = JsonDocument.Parse(json).RootElement;

            // Build the invocation options using the driver session, token,
            // tool catalog, and shared runtime services required by the G4 engine.
            var options = new InvokeOptions
            {
                DriverSession = schema.DriverSession,
                G4Client = client,
                HttpClient = _httpClient,
                Rule = ConvertToRule(arguments: jsonElement, schema.Intent, s_tools),
                Sessions = s_sessions,
                Token = schema.Token,
                Tools = s_tools
            };

            // Delegate the rule execution to the G4 engine and return its result.
            return SendRule(options);
        }

        /// <inheritdoc />
        public void SyncTools()
        {
            // Rebuild the tools collection from the cache manager.
            var rebuilt = new ConcurrentDictionary<string, McpToolModel>(FormatTools(cache));

            // Atomically replace the current tools collection with the rebuilt one.
            Interlocked.Exchange(ref s_tools, rebuilt);
        }

        // TODO: Implement logic to handle all types of G4 rules.
        // Converts the given tool name and parameters into a G4 rule model.
        // This method retrieves the plugin name associated with the tool and uses the parameters to create a rule model.
        private static ActionRuleModel ConvertToRule(
            JsonElement arguments,
            IntentModel intent,
            ConcurrentDictionary<string, McpToolModel> tools)
        {
            // Try to extract the rule data from the arguments JSON.
            var ruleData = arguments.TryGetProperty("rule", out var ruleOut)
                ? ruleOut
                : JsonDocument.Parse("{}").RootElement;

            // Retrieve the "name" of the tool to be invoked. Defaults to null if not provided.
            var includes = new[] { "tool_name", "toolName", "name", "tool" };
            var toolName = includes
                    .Select(k => ruleData.GetOrDefault(k, () => string.Empty)?.Trim())
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
                ?? string.Empty;

            // Retrieve the plugin name associated with the provided tool name from the internal tool registry (_tools)
            var pluginName = tools.GetValueOrDefault(key: toolName)?.QualifiedName ?? string.Empty;

            // Get the parameters from the ruleData, defaulting to an empty JSON object if not provided.
            var parameters = ruleData.TryGetProperty("toolParameters", out var parametersOut)
                ? parametersOut
                : JsonDocument.Parse("{}").RootElement;

            // Format the parameters into a command-line style string (e.g., "--param1:value1 --param2:value2").
            var parametersCli = FormatParameters(parameters);

            // Get the properties from the ruleData, defaulting to a JSON object with the plugin name if not provided.
            var properties = ruleData.TryGetProperty("toolProperties", out var propertiesOut)
                ? propertiesOut.GetRawText()
                : "{\"toolName\":\"" + pluginName + "\"}";

            // Deserialize the JSON string into the G4RuleModelBase object using the provided JSON options (JsonOptions)
            var rule = JsonSerializer.Deserialize<ActionRuleModel>(
                json: properties,
                options: AppSettings.JsonOptions);

            // Ensure the rule's Capabilities dictionary is initialized and add the intent to it.
            rule.Capabilities ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            rule.Capabilities["intent"] = intent;

            // Set the PluginName property of the rule to the retrieved plugin name
            rule.PluginName = pluginName;

            // Set the Argument property of the rule to the formatted parameters if provided
            rule.Argument = string.IsNullOrEmpty(parametersCli) || parametersCli.Equals("{{$ }}")
                ? rule.Argument
                : parametersCli;

            // Return the created rule
            return rule;

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
                var json = JsonSerializer.Serialize(value: parametersObject, options: AppSettings.JsonOptions);
                var parametersCollection = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    json,
                    options: AppSettings.JsonOptions)!;

                // Convert keys to PascalCase and render as --Key[:Value] (omit :Value when empty).
                var parametersExpression = parametersCollection
                    .Select(i =>
                        $"--{i.Key}" +
                        (string.IsNullOrEmpty(i.Value) ? "" : $":{i.Value}"))
                    .ToArray();

                // Wrap with G4 template delimiters.
                return "{{$ " + string.Join(" ", parametersExpression) + "}}";
            }
        }

        // Formats the tools available in the G4 framework, combining both
        // plugin-based tools and built-in system tools.
        private static ConcurrentDictionary<string, McpToolModel> FormatTools(CacheManager cache)
        {
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

            // Reads all embedded resources from the executing assembly and returns their
            // names together with their text content.
            static List<(string Name, string Content)> ReadSystemToolResource()
            {
                // Get the assembly that contains the currently executing code.
                var assembly = Assembly.GetExecutingAssembly();

                // Read the full list of embedded resource names available in the assembly.
                var resourceNames = assembly.GetManifestResourceNames();

                // Create the result collection that will hold the resource name and content pairs.
                var resources = new List<(string Name, string Content)>();

                // Iterate through all embedded resources exposed by the assembly.
                for (var i = 0; i < resourceNames.Length; i++)
                {
                    // Read the current resource name from the manifest.
                    var name = resourceNames[i];

                    // Filter the resources to include only those that are
                    // JSON files and are part of the "Resources.SystemTools" namespace.
                    var isJson = name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                    var isSystemTool = name.Contains("Resources.SystemTools", StringComparison.OrdinalIgnoreCase);

                    // If the current resource does not meet the criteria, skip to the next iteration.
                    if (!isJson || !isSystemTool)
                    {
                        continue;
                    }

                    // Try to open the embedded resource stream for the current resource name.
                    using var stream = assembly.GetManifestResourceStream(name);

                    // If the stream is null, it means the resource could not be found or opened.
                    if (stream == null)
                    {
                        continue;
                    }

                    // Read the entire resource content as UTF-8 text.
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    var content = reader.ReadToEnd();

                    // Add the resource name and its resolved content to the result list.
                    resources.Add((name, content));
                }

                // Return all collected resource name and content pairs.
                return resources;
            }

            // Reads the embedded system tool resource files, deserializes them into
            // McpClientTool instances, wraps them in McpToolModel
            static McpToolModel[] GetSystemTools()
            {
                // Read all embedded resources that may contain system tool definitions.
                var resources = ReadSystemToolResource();

                // Create the collection that will hold all successfully loaded system tools.
                var systemTools = new List<McpToolModel>();

                // Try to deserialize each embedded resource into a client tool and wrap it
                // in the internal system tool model.
                foreach (var (name, content) in resources)
                {
                    try
                    {
                        // Deserialize the current resource content into an MCP client tool.
                        var clientTool = JsonSerializer
                            .Deserialize<Tool>(content, AppSettings.JsonOptions);

                        // Create the internal tool model that wraps the deserialized client tool
                        // and exposes the normalized metadata used by the system.
                        var systemTool = new McpToolModel
                        {
                            ClientTool = clientTool,
                            QualifiedName = $"system.{clientTool.Name}",
                            Name = clientTool.Name,
                            Description = clientTool.Description,
                            Metadata = new()
                            {
                                Description = clientTool.Description,
                                Name = clientTool.Name
                            }
                        };

                        // Add the successfully created system tool to the result collection.
                        systemTools.Add(systemTool);
                    }
                    catch (Exception e)
                    {
                        // Log the resource name that failed so the invalid file can be identified.
                        Console.WriteLine($"Error deserializing system tool from resource '{name}'");

                        // Log the root error message for easier troubleshooting.
                        Console.WriteLine(e.GetBaseException().Message);

                        // Skip the invalid resource and continue processing the remaining ones.
                        continue;
                    }
                }

                // Return all successfully loaded system tools as an array.
                return [.. systemTools];
            }
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
                ["driverSession"] = session.Key,
                ["value"] = cleanHtml.ResolveSegments()
            };
        }

        // Retrieves the buffered rules associated with the session identifier
        // provided in the invocation arguments.
        private static List<(long Timestamp, G4RuleModelBase Rule)> GetBuffer(InvokeOptions options)
        {
            // Read the session identifier from the invocation arguments.
            // When the argument is missing, keep the value as null.
            var sessionId = options.Arguments.TryGetProperty("sessionId", out var sessionIdOut)
                ? sessionIdOut.GetString()
                : null;

            // Try to retrieve the buffered rules for the resolved session identifier.
            var buffer = options.Buffer.GetValueOrDefault(key: sessionId, defaultValue: null);

            // Reject the request when the session identifier is missing or when
            // no buffer exists for the requested session.
            if (string.IsNullOrEmpty(sessionId) || buffer == null)
            {
                var message = string.IsNullOrEmpty(sessionId)
                    ? "The sessionId argument is required to retrieve the current buffer."
                    : $"No buffer was found for sessionId '{sessionId}'. The session may be invalid, expired, or not initialized.";

                throw new InvalidSessionIdException(message);
            }

            // Return a materialized copy of the buffered rule entries.
            return [.. buffer];
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
        private static JsonElement ResolveLocator(InvokeOptions options)
        {
            // Fetch the current page DOM for the active driver session.
            var documentObject = GetApplicationDom(options);

            // Build the model input: carry over "intent" and inject the DOM so the model has page context.
            var intentObject = new Dictionary<string, object>
            {
                ["intent"] = options.Intent?.AgentIntent ?? string.Empty,
                ["dom"] = documentObject.GetValueOrDefault("value", "<html></html>"),
            };

            // Serialize the model input that will go into the user message content.
            var userContent = JsonSerializer.Serialize(
                value: intentObject,
                options: AppSettings.JsonOptions);

            // Prepare a Chat Completions–style payload (model + messages).
            // The system prompt is loaded from "LocatorSystemPrompt.md".
            var openAiRequest = new
            {
                Model = options.OpenaiModel,
                Messages = new[]
                {
                    new
                    {
                        Role = "system",
                        Content = InvokeOptions.SystemPrompts.Get(key: "LocatorSystemPrompt.md", defaultValue: "")
                    },
                    new
                    {
                        Role = "user",
                        Content = userContent
                    }
                }
            };

            // Serialize the request payload to JSON.
            var value = JsonSerializer.Serialize(
                value: openAiRequest,
                options: AppSettings.JsonOptions);

            // Create the HTTP POST request to the (OpenAI-compatible) endpoint.
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(options.OpenaiUri),
                Content = new StringContent(value, Encoding.UTF8, MediaTypeNames.Application.Json)
            };

            // Add bearer authentication with the provided API key.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.OpenaiApiKey);

            // Send the request synchronously (blocks the calling thread).
            // Consider SendAsync if this is on a request/UI thread.
            var response = options.HttpClient.Send(request);

            // Throw if the status is not success (non-2xx).
            response.EnsureSuccessStatusCode();

            // Read the response body as a raw JSON string.
            var responseBody = response.Content.ReadAsStringAsync().Result;

            // Parse the response JSON and extract choices[0].message.content.
            // This assumes the shape exists and contains JSON text payload.
            var document = JsonDocument.Parse(responseBody);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            // The model is expected to return JSON in message.content.
            // Deserialize that JSON into a JsonElement and return it.
            return JsonSerializer.Deserialize<JsonElement>(content, AppSettings.JsonOptions);
        }

        // Removes the buffer associated with the session identifier provided in the invocation arguments.
        // This is typically used to clean up resources when a session is closed or no longer needed.
        private static object RemoveBuffer(InvokeOptions options)
        {
            // Read the session identifier from the invocation arguments.
            // When the argument is missing, keep the value as null.
            var sessionId = options.Arguments.TryGetProperty("sessionId", out var sessionIdOut)
                ? sessionIdOut.GetString()
                : null;

            // Try to remove the buffer for the resolved session identifier.
            var removed = !string.IsNullOrEmpty(sessionId) && options.Buffer.TryRemove(sessionId, out _);

            // Return an indication of whether the buffer was successfully removed.
            return new
            {
                Removed = removed,
                SessionId = sessionId
            };
        }

        // Removes the session associated with the session identifier provided in the invocation arguments.
        // This is typically used to clean up resources when a session is closed or no longer needed.
        private static object RemoveSession(InvokeOptions options)
        {
            // Read the session identifier from the invocation arguments.
            // When the argument is missing, keep the value as null.
            var sessionId = options.Arguments.TryGetProperty("sessionId", out var sessionIdOut)
                ? sessionIdOut.GetString()
                : null;

            // Try to remove the session for the resolved session identifier.
            var removed = !string.IsNullOrEmpty(sessionId) && options.Sessions.TryRemove(sessionId, out _);

            // Return an indication of whether the session was successfully removed.
            return new
            {
                Removed = removed,
                SessionId = sessionId
            };
        }

        // Starts the automation rule execution by invoking the specified rule on the client,
        // and retrieves the response, including the driver session and the value of the executed rule.
        private static object SendRule(InvokeOptions options)
        {
            // Create a new automation model using the provided driver binaries, authentication token, and rule.
            var automation = NewAutomation(options.DriverSession, options.Token, options.Rule);

            // Invoke the automation process using the client and retrieve the response.
            var response = options.G4Client.Automation.Invoke(automation);

            // Extract the session object from the response, ensuring we track the latest session.
            var session = response.Values.Last().Sessions.Last();

            // Add the session to the sessions dictionary to keep track of the active session.
            options.Sessions[session.Key] = session.Value;

            // Add the executed rule to the buffer associated with the session key.
            var buffer = options.Buffer.GetValueOrDefault(key: session.Key, defaultValue: null);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            buffer?.Add((timestamp, options.Rule));

            // Retrieve the response for the rule execution from the response tree structure.
            var ruleResponse = session
                .Value
                .ResponseTree
                .Stages.Last()
                .Jobs.Last()
                .Plugins
                .Last();

            // Get the current buffer for the session, defaulting to an empty list if none exists.
            var bufferOut = buffer ?? [];

            // If the rule is to close the browser, clean up the session and buffer.
            if (options.Rule.PluginName.Equals("CloseBrowser", StringComparison.OrdinalIgnoreCase))
            {
                // Remove the session from the sessions dictionary when closing the browser.
                options.Sessions.TryRemove(session.Key, out _);
                
                // Clear the buffer associated with the session key.
                options.Buffer.TryRemove(session.Key, out bufferOut);
            }

            // Return an anonymous object containing the session key and the rule response value.
            return new
            {
                DriverSession = session.Key,
                Value = new
                {
                    Buffer = bufferOut.OrderBy(i => i.Timestamp).Select(i => i.Rule),
                    Response = ruleResponse
                }
            };
        }

        // Starts a new G4 session by invoking the automation process with the provided parameters.
        // The session information is returned, and the session is added to the provided dictionary of active sessions.
        private static object StartSession(InvokeOptions options)
        {
            // Parse the "arguments" into a JSON string for further processing.
            var arguments = options.Arguments.ToString();

            // Define the driver parameters, including the driver name and the path to the binaries.
            // The driver name (e.g., Chrome, Firefox).
            // The path to the driver binaries or the Selenium Grid URL.
            var driverParameters = JsonSerializer.Deserialize<Dictionary<string, object>>(arguments);

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

            // Initialize a new concurrent bag for buffering rules associated with this session.
            options.Buffer[session.Key] = [];

            // Return an anonymous object containing the session key.
            // The session key for the newly created session.
            return new
            {
                DriverSession = session.Key
            };
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents a scored example returned from the cached example search operation.
        /// </summary>
        /// <remarks>
        /// This model contains the original rule example together with its calculated
        /// relevance score and the extracted tool properties and parameters derived
        /// from the example rule.
        /// </remarks>
        public sealed class ExamplesResultModel
        {
            /// <summary>
            /// Gets or sets the rule example returned from the cache search.
            /// </summary>
            public RuleExampleModel Rule { get; set; }

            /// <summary>
            /// Gets or sets the relevance score assigned to the matched example.
            /// </summary>
            public int Score { get; set; }

            /// <summary>
            /// Gets or sets the parsed tool parameters extracted from the rule argument
            /// in G4 CLI format.
            /// </summary>
            public Dictionary<string, string> ToolParameters { get; set; }

            /// <summary>
            /// Gets or sets the exported tool properties extracted from the example rule.
            /// </summary>
            public Dictionary<string, object> ToolProperties { get; set; }
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
                DriverBinaries = arguments.GetOrDefault("driverBinaries", () => default(string));

                // Retrieve the "driverSession" argument (existing session ID for reusing a browser session).
                // Defaults to null if not provided.
                DriverSession = arguments.GetOrDefault("driverSession", () => default(string));

                // Retrieve the "intent" argument, which describes the purpose of
                // the invocation for semantic lookup.
                Intent = arguments.GetOrDefault("intent", () => default(IntentModel));

                // Retrieve the OpenAI API key for authentication with the OpenAI API.
                // Defaults to null if not provided.
                OpenaiApiKey = arguments.GetOrDefault("openaiApiKey", () => default(string));

                // Retrieve the OpenAI model identifier (e.g., gpt-4, gpt-5).
                // Defaults to null if not provided.
                OpenaiModel = arguments.GetOrDefault("openaiModel", () => default(string));

                // Retrieve the OpenAI API base URI (custom endpoint if applicable).
                // Defaults to null if not provided.
                OpenaiUri = arguments.GetOrDefault("openaiUri", () => default(string));

                // Retrieve the "token" argument (general-purpose authentication or API token).
                // Defaults to null if not provided.
                Token = arguments.GetOrDefault("token", () => default(string));

                // Retrieve the "name" argument (the tool identifier to be invoked).
                // Defaults to null if not provided.
                ToolName = parameters.GetOrDefault("name", () => default(string));
            }

            /// <summary>
            /// Gets the system prompts used for tool invocation, loaded from embedded resources.
            /// </summary>
            public static ReadOnlyDictionary<string, string> SystemPrompts = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LocatorSystemPrompt.md"] = ReadSystemPrompt(instrcutionsManifest: "LocatorSystemPrompt.md")
            });

            /// <summary>
            /// The raw JSON <c>"arguments"</c> object supplied to the tool.
            /// </summary>
            public JsonElement Arguments { get; set; }

            /// <summary>
            /// Gets or sets the buffer that tracks recently executed rules for each session.
            /// </summary>
            public ConcurrentDictionary<string, ConcurrentBag<(long Timestamp, G4RuleModelBase Rule)>> Buffer { get; set; }

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
            public IntentModel Intent { get; set; }

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
    }
}
