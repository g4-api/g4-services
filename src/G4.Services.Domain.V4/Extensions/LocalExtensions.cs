using G4.Models;

using HtmlAgilityPack;

using Microsoft.AspNetCore.SignalR;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;


namespace G4.Extensions
{
    internal static class LocalExtensions
    {
        /// <summary>
        /// Recursively cleans an HTML node by removing unwanted elements and leaving only the specified tags.
        /// The tags to be kept are defined in the `includedTags` set, and all other elements will be removed.
        /// </summary>
        /// <param name="node">The root HTML node to clean.</param>
        /// <returns>The cleaned HTML node with unwanted elements removed.</returns>
        public static HtmlNode Clean(this HtmlNode node)
        {
            // Define a set of allowed tags that should not be removed.
            var includedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "head", "title", "base", "noscript", "script", "style", "link", "meta", "frame",
                "frameset", "object", "embed", "param", "source", "track", "picture", "canvas", "map", "area"
            };

            // Iterate through all child nodes of the current node.
            foreach (var child in node.ChildNodes.ToList())
            {
                // Skip text nodes, as we don't want to remove them.
                if (child.NodeType == HtmlNodeType.Text)
                {
                    continue;
                }

                // Determine whether the current child node is an element.
                var isElement = child.NodeType == HtmlNodeType.Element;

                // Check if the current element is in the included tags list.
                var isIncludedTag = isElement && includedTags.Contains(child.Name);

                if (isElement && isIncludedTag)
                {
                    // If it's an element that should be included, remove it completely (e.g., script, style).
                    node.RemoveChild(child);
                }
                else if (isElement)
                {
                    // If it's an element that should be cleaned, recursively clean its child nodes.
                    Clean(child);
                }
                else
                {
                    // For non-element nodes, simply remove them.
                    child.Remove();
                }
            }

            // Return the cleaned node after removing unwanted children.
            return node;
        }

        /// <summary>
        /// Converts a plugin manifest into an <see cref="McpToolModel"/>, 
        /// extracting its name, description, and input schema (parameters & properties).
        /// </summary>
        /// <param name="manifest">The plugin manifest containing metadata, parameters, and properties.</param>
        /// <returns>A fully populated <see cref="McpToolModel"/> representing the same plugin, ready for JSON‐RPC schema generation.
        /// </returns>
        public static McpToolModel ConvertToTool(this IG4PluginManifest manifest)
        {
            // Converts PluginParameterModel to McpToolModel.ScehmaPropertyModel for input schema
            static McpToolModel.ScehmaPropertyModel ConvertToInputSchema(PluginParameterModel parameterModel)
            {
                // Converts various naming styles (kebab-case, camelCase) to snake_case
                static string ConvertToSnakeCase(string input) => input
                    .Replace("-", " ")
                    .ConvertToKebabCase()
                    .Replace("-", "_")
                    .ToLower();

                // Parses and normalizes the 'Type' string into valid JSON schema types
                static string[] ConvertType(string input)
                {
                    // List of approved JSON schema types for validation and normalization
                    var approvedTypes = new[] { "null", "boolean", "object", "array", "number", "integer", "string" };

                    // Create a set of approved types for input validation and normalization
                    return [.. input
                        .Split('|')
                        .Select(i => (approvedTypes.Contains(i, StringComparer.OrdinalIgnoreCase) ? i.Trim().ToLower() : "string"))
                        .Distinct()
                        .Select(i => (i == "switch" || i == "bool") ? "boolean" : i)
                        .Distinct()];
                }

                // Construct the schema property model from the parameter metadata
                // Return the created schema property and its 'Mandatory' flag
                var schema = new McpToolModel.ScehmaPropertyModel
                {
                    Description = string.Join(" ", parameterModel.Description ?? []),
                    //Enum = [],
                    Name = ConvertToSnakeCase(parameterModel.Name),
                    G4Required = parameterModel.Mandatory,
                    Type = ConvertType(parameterModel.Type)
                };

                // If the parameter is "Rules", add a specific schema for nested rules
                if (parameterModel.Name.Equals("Rules", StringComparison.OrdinalIgnoreCase))
                {
                    schema.Items = new McpToolModel.ParameterSchemaModel
                    {
                        Description = "Array of nested rule objects—each item’s tool_name must first be looked up via find_tool to " +
                            "retrieve its inputSchema before its parameters are included in the parent rule for invoke_g4_tool to process as one.",
                        Type = "object"
                    };
                }

                // Return the constructed schema property model
                return schema;
            }

            // Derive a standardized plugin key by replacing non-word characters with spaces.
            var pluginKey = Regex.Replace(manifest.Key, pattern: @"\W+", replacement: " ");

            // Derive a standardized tool name: replace hyphens with spaces,
            // kebab‑case it, then turn hyphens into underscores and lowercase all.
            var name = pluginKey
                .Replace("-", " ")
                .ConvertToKebabCase()
                .Replace("-", "_")
                .ToLower();

            // Join all summary lines into a single human-readable description.
            var description = string.Join(' ', manifest.Summary);

            // Convert each declared parameter in the manifest into a schema property.
            var pluginParameters = (manifest.Parameters ?? [])
                .Select(ConvertToInputSchema)
                .ToDictionary(i => i.Name, i => i);

            // Likewise convert any additional manifest properties into schema properties.
            var pluginProperties = (manifest.Properties ?? [])
                .Select(ConvertToInputSchema)
                .ToDictionary(i => i.Name, i => i);

            // Build and return the final MCP tool model.
            return new McpToolModel
            {
                Description = description,
                G4Name = manifest.Key,
                Name = name,
                Type = "g4-tool",
                InputSchema = new McpToolModel.ParameterSchemaModel
                {
                    Type = "object",
                    Properties = new()
                    {
                        ["properties"] = new()
                        {
                            Type = ["object"],
                            Description = "Standard input fields defined by the G4 Engine API. " +
                                "These provide the core operational context for the tool, such as element locators, attributes, and matching rules.",
                            Properties = pluginProperties,
                            Required = [.. pluginProperties.Where(i => i.Value.G4Required).Select(i => i.Value.Name)]
                        },
                        ["parameters"] = new()
                        {
                            Type = ["object"],
                            Description = "Custom, tool-specific parameters that extend or refine the tool's functionality. " +
                                "These may include additional metadata, behavior modifiers, or configuration settings unique to this tool’s purpose.",
                            Properties = pluginParameters,
                            Required = [.. pluginParameters.Where(i => i.Value.G4Required).Select(i => i.Value.Name)]
                        }
                    },
                    Required = []
                }
            };
        }

        /// <summary>
        /// Retrieves the connection identifier from the G4AutomationModel's SignalR environment settings.
        /// </summary>
        /// <param name="automation">The G4AutomationModel instance containing the environment settings.</param>
        /// <returns>The connection identifier if it exists in the SignalR environment settings; otherwise, an empty string.</returns>
        public static string GetConnection(this G4AutomationModel automation)
        {
            // Retrieve the collection of environment variables from the automation settings.
            var environmentVariables = automation?.Settings?.EnvironmentsSettings?.EnvironmentVariables;

            // Check if the EnvironmentVariables collection exists.
            bool environmentVariablesExist = environmentVariables != null;

            // Verify that the "SignalR" key is present in the EnvironmentVariables collection.
            bool containsSignalREntry = environmentVariablesExist && environmentVariables.ContainsKey("SignalR");

            // Confirm that the "SignalR" entry includes a non-null Parameters dictionary.
            bool hasParameters = containsSignalREntry && environmentVariables["SignalR"]?.Parameters != null;

            // Determine if the Parameters dictionary contains the "ConnectionId" key.
            bool containsConnectionId = hasParameters && environmentVariables["SignalR"].Parameters.ContainsKey("ConnectionId");

            // Return the connection identifier if available; otherwise, return an empty string.
            return containsConnectionId
                ? $"{environmentVariables["SignalR"].Parameters["ConnectionId"]}"
                : string.Empty;
        }

        /// <summary>
        /// Retrieves the value of the <c>"token"</c> property from a <see cref="JsonElement"/>, 
        /// or falls back to a default value provided by the factory function if the property does not exist 
        /// or cannot be converted to the target type.
        /// </summary>
        /// <typeparam name="T">The expected type of the "token" value. Supports <see cref="string"/>, <see cref="int"/>, and <see cref="bool"/>.</typeparam>
        /// <param name="jsonElement">The <see cref="JsonElement"/> that may contain the "token" property.</param>
        /// <param name="propertyName"> The name of the property to retrieve.</param>
        /// <param name="defaultValue">A function that produces a default value when the "token" property is missing or not supported.</param>
        /// <returns>Returns the value of the "token" property if found and successfully converted; otherwise, returns the value produced by the <paramref name="defaultValue"/>.</returns>
        public static T GetOrDefault<T>(this JsonElement jsonElement, string propertyName, Func<T> defaultValue)
        {
            // Try to get the "token" property from the JSON element.
            // This could represent an authentication token or API key.
            var isToken = jsonElement.TryGetProperty(propertyName, out var tokenOut);

            // If the "token" property does not exist, return the default value from the factory.
            if (!isToken)
            {
                return defaultValue();
            }

            // If the "token" property exists, decide how to extract its value
            // based on its JSON value type.
            return tokenOut.ValueKind switch
            {
                // If it's a string, cast to object first, then to generic type T.
                JsonValueKind.String => (T)(object)tokenOut.GetString(),

                // If it's a number, interpret it as a 32-bit integer.
                JsonValueKind.Number => (T)(object)tokenOut.GetInt32(),

                // If it's a boolean true, return true.
                JsonValueKind.True => (T)(object)true,

                // If it's a boolean false, return false.
                JsonValueKind.False => (T)(object)false,

                // If it's any other type (array, object, null, undefined, etc.), 
                // fall back to the default value from the factory.
                _ => defaultValue()
            };
        }

        /// <summary>
        /// Sends a message to a specific client using the SignalR hub context.
        /// </summary>
        /// <typeparam name="T">The type of the Hub.</typeparam>
        /// <param name="context">The hub context used to send the message.</param>
        /// <param name="connectionId">The unique identifier for the target client connection.</param>
        /// <param name="method">The name of the method to be invoked on the client.</param>
        /// <param name="message">The message payload to be sent.</param>
        public static void SendMessage<T>(this IHubContext<T> context, string connectionId, string method, object message)
            where T : Hub
        {
            // If the connection ID is null or empty, do not proceed with sending the message.
            if (string.IsNullOrEmpty(connectionId))
            {
                return;
            }

            // Send the specified message to the client with the given connection ID using the provided method.
            context.Clients.Client(connectionId).SendAsync(method, message);
        }
    }
}
