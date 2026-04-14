using G4.Models;
using G4.Settings;

using HtmlAgilityPack;

using Microsoft.AspNetCore.SignalR;
using Microsoft.OpenApi;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

//using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;


namespace G4.Extensions
{
    internal static class LocalExtensions
    {
        extension(G4AutomationModel automation)
        {
            /// <summary>
            /// Retrieves the connection identifier from the G4AutomationModel's SignalR environment settings.
            /// </summary>
            /// <returns>The connection identifier if it exists in the SignalR environment settings; otherwise, an empty string.</returns>
            public string GetConnection()
            {
                // Retrieve the collection of environment variables from the automation settings.
                var environmentVariables = automation?
                    .Settings?
                    .EnvironmentsSettings?
                    .EnvironmentVariables;

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
        }

        extension(HtmlNode node)
        {
            /// <summary>
            /// Recursively cleans an HTML node by removing unwanted elements and leaving only the specified tags.
            /// The tags to be kept are defined in the `includedTags` set, and all other elements will be removed.
            /// </summary>
            /// <returns>The cleaned HTML node with unwanted elements removed.</returns>
            public HtmlNode Clean()
            {
                // Define a set of allowed tags that should not be removed.
                var includedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
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
        }

        extension(IG4PluginManifest manifest)
        {
            /// <summary>
            /// Converts the current manifest into an MCP tool model.
            /// </summary>
            /// <returns>A fully constructed <see cref="McpToolModel"/> built from the current manifest.</returns>
            /// <remarks>
            /// This method normalizes the manifest key into an MCP-safe tool name, builds the
            /// input schema from both manifest parameters and properties, and creates the final
            /// client-facing tool definition together with the internal metadata wrapper.
            /// </remarks>
            public McpToolModel ConvertToTool()
            {
                // Normalize the manifest key by replacing non-word characters with spaces.
                // This creates a stable intermediate value that can later be converted into
                // the target MCP tool naming convention.
                var pluginKey = Regex.Replace(manifest.Key, pattern: @"\W+", replacement: " ");

                // Convert the normalized key into the final MCP tool name.
                // The result is lowercase and uses underscores for separation.
                var name = pluginKey
                    .Replace("-", " ")
                    .ConvertToKebabCase()
                    .Replace("-", "_")
                    .ToLower();

                // Combine the manifest summary lines into a single description string
                // for the tool and client model.
                var description = string.Join(' ', manifest.Summary);

                // Convert all declared manifest parameters into schema entries keyed by name.
                var pluginParameters = (manifest.Parameters ?? [])
                    .Select(ConvertToInputSchema)
                    .ToDictionary(i => i.Name, i => i);

                // Convert all declared manifest properties into schema entries keyed by name.
                var pluginProperties = (manifest.Properties ?? [])
                    .Select(ConvertToInputSchema)
                    .ToDictionary(i => i.Name, i => i);

                // Build the schema map for the Properties section.
                // Property names are normalized to camelCase for the final input schema.
                var properties = pluginProperties.ToDictionary(i => i.Value.Name.ConvertToCamelCase(), i => i.Value.Schema);

                // Collect the required property names from the converted property schema entries.
                var requiredProperties = pluginProperties
                    .Where(i => i.Value.Required)
                    .Select(i => i.Value.Name)
                    .ToArray();

                // Build the schema map for the Parameters section.
                var parameters = pluginParameters.ToDictionary(i => i.Value.Name, i => i.Value.Schema);

                // Collect the required parameter names from the converted parameter schema entries.
                var requiredParameters = pluginParameters
                    .Where(i => i.Value.Required)
                    .Select(i => i.Value.Name)
                    .ToArray();

                // Build the input schema payload expected by the MCP client tool model.
                // The schema is divided into two top-level sections:
                // 1. Properties  - standard G4 engine fields.
                // 2. Parameters  - tool-specific custom inputs.
                var inputSchema = new
                {
                    Type = "object",
                    Properties = new
                    {
                        Type = "object",
                        Description = "Standard input fields defined by the G4 Engine API. " +
                            "These provide the core operational context for the tool, such as element locators, attributes, and matching rules.",
                        Properties = properties,
                        Required = requiredProperties

                    },
                    Parameters = new
                    {
                        Type = "object",
                        Description = "Custom, tool-specific parameters that extend or refine the tool's functionality. " +
                            "These may include additional metadata, behavior modifiers, or configuration settings unique to this tool’s purpose.",
                        Properties = parameters,
                        Required = requiredParameters
                    },
                    Required = Array.Empty<string>()
                };

                // Serialize the generated schema object into JSON so it can be parsed
                // into a JsonElement for the MCP client tool definition.
                var json = JsonSerializer.Serialize(
                    value: inputSchema,
                    options: AppSettings.JsonOptions);

                // Create and return the final MCP tool model.
                // This includes:
                // - the internal G4-facing metadata,
                // - the normalized MCP tool name,
                // - the human-readable description,
                // - and the client tool definition with its generated input schema.
                return new McpToolModel
                {
                    Description = description,
                    G4Name = manifest.Key,
                    Name = name,
                    Metadata = new McpToolModel.ToolMetadataModel
                    {
                        Description = string.Join(Environment.NewLine, manifest.Summary),
                        Name = name,
                        Type = "g4-tool"
                    },
                    Type = "g4-tool",
                    ClientTool = new()
                    {
                        Description = description,
                        InputSchema = JsonElement.Parse(json),
                        Name = name,
                        OutputSchema = null,
                        // TODO: Take from context.integration
                        Title = name,
                    }
                };

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
                        .Distinct()
                    ];
                }

                // Converts a plugin parameter definition into an input schema element
                // that can be used in the generated tool schema.
                static (string Name, bool Required, JsonElement Schema) ConvertToInputSchema(PluginParameterModel parameterModel)
                {
                    // Check whether the current parameter represents the special Rules collection,
                    // which requires an item schema for nested rule objects.
                    var isRules = parameterModel.Name.Equals("Rules", StringComparison.OrdinalIgnoreCase);

                    // Build the schema payload from the plugin parameter metadata.
                    // The parameter name is normalized to snake_case, the type is mapped
                    // into the target schema type, and the required flag is preserved.
                    var schema = new
                    {
                        Description = string.Join(" ", parameterModel.Description ?? []),
                        Type = ConvertType(parameterModel.Type),
                        Items = isRules ? new
                        {
                            Description = "Array of nested rule objects—each item’s tool_name must first be looked up via find_tool to " +
                                "retrieve its inputSchema before its parameters are included in the parent rule for invoke_g4_tool to process as one.",
                            Type = "object"
                        } : null
                    };

                    // Serialize the anonymous schema object into JSON using the shared serializer options.
                    var json = JsonSerializer.Serialize(schema, AppSettings.JsonOptions);

                    // Parse the JSON payload into a JsonElement and return it.
                    return (parameterModel.Name, parameterModel.Mandatory, JsonElement.Parse(json));
                }
            }
        }

        extension<T>(IHubContext<T> context) where T : Hub
        {
            /// <summary>
            /// Sends a message to a specific client using the SignalR hub context.
            /// </summary>
            /// <param name="connectionId">The unique identifier for the target client connection.</param>
            /// <param name="method">The name of the method to be invoked on the client.</param>
            /// <param name="message">The message payload to be sent.</param>
            public void SendMessage(string connectionId, string method, object message)
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

        extension(JsonElement jsonElement)
        {
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
            public T GetOrDefault<T>(string propertyName, Func<T> defaultValue)
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
        }

        extension(OpenApiInfo info)
        {
            /// <summary>
            /// Adds G4 platform contact information to the OpenAPI info.
            /// </summary>
            /// <returns>The <see cref="OpenApiInfo"/> instance for chaining.</returns>
            public OpenApiInfo AddG4Contact()
            {
                // Set the contact details for G4 support
                info.Contact = new OpenApiContact()
                {
                    Name = "G4 Support",
                    Email = "g4.platforms@gmail.com",
                    Url = new Uri("https://github.com/g4-api/g4-docs")
                };

                // Return the modified OpenApiInfo instance for chaining.
                return info;
            }

            /// <summary>
            /// Adds the G4 license information (MIT) to the OpenAPI info.
            /// </summary>
            /// <returns>The <see cref="OpenApiInfo"/> instance for chaining.</returns>
            public OpenApiInfo AddG4License()
            {
                // Set the license details for G4
                info.License = new OpenApiLicense()
                {
                    Identifier = "MIT",
                    Name = "MIT License",
                    Url = new Uri("https://opensource.org/license/mit/")
                };

                // Return the modified OpenApiInfo instance for chaining.
                return info;
            }

            /// <summary>
            /// Adds the G4 terms of service URL to the OpenAPI info.
            /// </summary>
            /// <returns>The <see cref="OpenApiInfo"/> instance for chaining.</returns>
            public OpenApiInfo AddG4TermsOfService()
            {
                // Set the terms of service link for G4
                info.TermsOfService = new Uri("https://github.com/g4-api/g4-docs/blob/main/responsible-use/usage-policy.md");

                // Return the modified OpenApiInfo instance for chaining.
                return info;
            }

            /// <summary>
            /// Adds the ™ symbol to "G4" in the OpenAPI info title.
            /// </summary>
            /// <returns>The <see cref="OpenApiInfo"/> instance for chaining.</returns>
            public OpenApiInfo AddG4Trademark()
            {
                // Replace "G4" in the title with "G4™" to add the trademark symbol
                info.Title = info.Title.Replace("G4", "G4™");

                // Return the modified OpenApiInfo instance for chaining.
                return info;
            }
        }
    }
}
