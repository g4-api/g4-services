using G4.Cache;
using G4.Models;

using HtmlAgilityPack;

using Microsoft.AspNetCore.SignalR;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;


namespace G4.Extensions
{
    internal static class LocalExtensions
    {

        public static HtmlNode Clean(this HtmlNode node)
        {
            var includedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "head",
                "title",
                "base",
                "noscript",
                "script",
                "style",
                "link",
                "meta",
                "frame",
                "frameset",
                "object",
                "embed",
                "param",
                "source",
                "track",
                "picture",
                "canvas",
                "map",
                "area"
            };

            foreach (var child in node.ChildNodes.ToList())
            {
                if(child.NodeType == HtmlNodeType.Text)
                {
                    continue;
                }

                var isElement = child.NodeType == HtmlNodeType.Element;
                var isIncludedTag = isElement && includedTags.Contains(child.Name);
                
                if (isElement && isIncludedTag)
                {
                    // Remove the entire script or style tag
                    node.RemoveChild(child);
                }
                else if (isElement)
                {
                    // Recursively clean child nodes
                    Clean(child);
                }
                else
                {
                    child.Remove();
                }
            }

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
                    Description = string.Join(" ", parameterModel.Description ?? Array.Empty<string>()),
                    //Enum = [],
                    Name = ConvertToSnakeCase(parameterModel.Name),
                    Required = parameterModel.Mandatory,
                    Type = ConvertType(parameterModel.Type)
                };

                // If the parameter is "Rules", add a specific schema for nested rules
                if (parameterModel.Name.Equals("Rules", StringComparison.OrdinalIgnoreCase))
                {
                    schema.Items = new McpToolModel.ParameterSchemaModel
                    {
                        Description = "Array of nested rule objects—each item’s tool_name must first be looked up via find_tool to retrieve its inputSchema before its parameters are included in the parent rule for invoke_g4_tool to process as one.",
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

            // Define the set of approved JSON schema primitive types.
            // (Note: currently not filtered against manifest.Type, but kept for future use.)
            var included = new[] { "null", "boolean", "object", "array", "number", "integer", "string" };

            // Convert each declared parameter in the manifest into a schema property.
            var pluginParameters = (manifest.Parameters ?? []).Select(ConvertToInputSchema);

            // Likewise convert any additional manifest properties into schema properties.
            var pluginProperties = (manifest.Properties ?? []).Select(ConvertToInputSchema);

            // Combine parameters and properties into a single lookup by property name.
            var properties = pluginParameters
                .Concat(pluginProperties)
                .ToDictionary(prop => prop.Name, prop => prop);

            // Determine which properties are required based on the schema metadata.
            var requiredProperties = properties.Values
                .Where(prop => prop.Required)
                .Select(prop => prop.Name)
                .ToArray();

            // Build and return the final MCP tool model.
            return new McpToolModel
            {
                Description = description,
                G4Name = manifest.Key,
                Name = name,
                InputSchema = new McpToolModel.ParameterSchemaModel
                {
                    Type = "object",
                    Properties = properties,
                    Required = requiredProperties
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
