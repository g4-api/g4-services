using G4.Attributes;
using G4.Cache;
using G4.Models;
using G4.Plugins.Engine;
using G4.Services.Domain.V4.Models;
using G4.Services.Domain.V4.Repositories;
using G4.Settings.Models;
using G4.WebDriver.Remote;
using G4.WebDriver.Simulator;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.SignalR;

using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace G4.Extensions
{
    /// <summary>
    /// Provides extension methods for handling <see cref="IG4PluginManifest"/> objects, allowing for dynamic field extraction.
    /// </summary>
    public static class PublicExtensions
    {
        // JSON serializer options with case-insensitive property name matching.
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        extension(ApiDescription description)
        {
            /// <summary>
            /// Determines whether this API description should be included in the specified G4 OpenAPI document.
            /// </summary>
            /// <param name="name">The name of the OpenAPI document to check against.</param>
            /// <returns><c>true</c> if the API description passes the filter for the document or if the document is not found; otherwise, <c>false</c>.</returns>
            public bool FilterG4Documents(string name)
            {
                // If the document is not found in the collection, allow it by default
                if (!OpenApiDocuments.Documents.TryGetValue(name, out OpenApiManager.DocumentDataModel document))
                {
                    return true;
                }

                // Use OpenApiManager to assert if the description matches any of the document's tags
                return OpenApiManager.AssertDescription(description, document.Tags);
            }
        }

        extension(G4AutomationModel automation)
        {
            /// <summary>
            /// Re-initializes the provided <see cref="G4AutomationModel"/> and invokes a macro resolver to process
            /// all of its <see cref="G4RuleModelBase"/> rules.
            /// </summary>
            /// <returns>A collection of <see cref="G4RuleModelBase"/> objects whose macros have been resolved.</returns>
            public IEnumerable<G4RuleModelBase> ResolveMacros()
            {
                // Re-initialize the G4AutomationModel using the global cache 
                // to ensure all references and actions are properly set up.
                automation = automation.Initialize(CacheManager.Instance);

                // Create a new AutomationInvoker to handle the logic for executing and resolving actions in the automation model.
                var invoker = new AutomationInvoker(automation);

                // Retrieve the plugin factory needed for macro resolution.
                var factory = invoker.PluginFactoryAdapter.MacroPluginFactory;

                // Attempt to extract a session ID from the DriverParameters using a RegEx pattern. 
                // If no session ID is found, default to "-1".
                var session = Regex.Match(automation.DriverParameters.Get("driver", string.Empty), "(?<=Id\\().*(?=\\))").Value;
                var driverSession = string.IsNullOrEmpty(session) ? "-1" : session;

                // Gather all the rules from the automation model by iterating over its stages and jobs.
                var rules = automation.Stages
                    .SelectMany(stage => stage.Jobs)
                    .SelectMany(job => job.Rules);

                // Check if there is an active IWebDriver associated with the session ID; otherwise, use a SimulatorDriver.
                var driver = AutomationInvoker.Drivers.TryGetValue(driverSession, out IWebDriver driverOut)
                    ? driverOut
                    : new SimulatorDriver();

                // Get the currently executing assembly and all referenced assemblies so we can find the MacroResolver type.
                var assembly = Assembly.GetExecutingAssembly();
                var assemblies = assembly.GetReferencedAssemblies();

                // Locate the MacroResolver type among the referenced assemblies.
                var type = assemblies
                    .SelectMany(name => Assembly.Load(name).GetTypes())
                    .FirstOrDefault(t => t.Name == "MacroResolver");

                // Create an instance of the MacroResolver, passing in our driver and plugin factory as constructor arguments.
                var macroResolver = Activator.CreateInstance(type, [driver, factory]);

                // Retrieve a reference to the "Resolve" method on the MacroResolver type.
                var resolve = type.GetMethod("Resolve");

                // Prepare a list to hold our newly resolved rules.
                var resolvedRules = new List<G4RuleModelBase>();

                // For each rule, invoke the MacroResolver's Resolve method, then add the resolved rule to our list.
                foreach (var rule in rules)
                {
                    var resolvedRule = (G4RuleModelBase)resolve.Invoke(macroResolver, [rule]);
                    resolvedRules.Add(resolvedRule);
                }

                // Return the list of rules that have had their macros resolved.
                return resolvedRules;
            }
        }

        extension(HttpRequestMessage request)
        {
            /// <summary>
            /// Sets the Authorization header on an <see cref="HttpRequestMessage"/> using the specified scheme and parameter.
            /// </summary>
            /// <param name="scheme">The authentication scheme (e.g., "Bearer").</param>
            /// <param name="parameter">The token or credentials used for authentication.</param>
            /// <returns>The original <see cref="HttpRequestMessage"/> with the Authorization header set.</returns>
            public HttpRequestMessage SetAuthorization(string scheme, string parameter)
            {
                // Apply the Authorization header to the request (e.g., Authorization: Bearer <token>)
                request.Headers.Authorization = new AuthenticationHeaderValue(scheme, parameter);

                // Return the modified request to allow for fluent method chaining
                return request;
            }
        }

        extension(HubOptions options)
        {
            /// <summary>
            /// Applies the configured hub settings to the target <see cref="HubOptions"/> instance.
            /// </summary>
            /// <param name="settings">The settings model that provides the hub option values to apply.</param>
            /// <returns>The updated <see cref="HubOptions"/> instance.</returns>
            /// <remarks>
            /// This method copies the supported runtime hub settings from the supplied
            /// <see cref="HubOptionsSettings"/> model into the target SignalR
            /// <see cref="HubOptions"/> instance and applies default values when needed.
            /// </remarks>
            public HubOptions New(HubOptionsSettings settings)
            {
                // Enable or disable detailed server-side errors.
                // Default to true when the setting is not provided.
                options.EnableDetailedErrors = settings.EnableDetailedErrors ?? true;

                // Set the maximum allowed size for a single incoming hub message.
                // Default to the largest possible value when not configured.
                options.MaximumReceiveMessageSize = settings.MaximumReceiveMessageSize ?? long.MaxValue;

                // Configure how often the server sends keep-alive pings to connected clients.
                // Default to 15 seconds when not explicitly configured.
                options.KeepAliveInterval = settings.KeepAliveInterval ?? TimeSpan.FromSeconds(15);

                // Configure how long the server waits before considering a client disconnected.
                // Default to 60 seconds when not explicitly configured.
                options.ClientTimeoutInterval = settings.ClientTimeoutInterval ?? TimeSpan.FromSeconds(60);

                // Return the updated hub options instance.
                return options;
            }
        }

        extension(IG4PluginManifest manifest)
        {
            /// <summary>
            /// Extracts specific fields from the <see cref="IG4PluginManifest"/> based on a comma-separated list of field names.
            /// </summary>
            /// <param name="expandFields">A comma-separated list of field names to extract.</param>
            /// <returns>A new <see cref="IG4PluginManifest"/> containing only the specified fields, or the original manifest if no fields are specified.</returns>
            public IG4PluginManifest ExtractFields(string expandFields)
            {
                // Check if no fields are specified, return the original manifest.
                if (string.IsNullOrEmpty(expandFields))
                {
                    return manifest;
                }

                // Split the comma-separated fields, trim whitespace, and convert them to an array.
                var expandFieldsArray = expandFields.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(field => field.Trim())
                    .ToArray();

                // Call the overload method to extract fields based on the array of field names.
                return ExtractFields(manifest, expandFields: expandFieldsArray);
            }

            /// <summary>
            /// Extracts specific fields from the <see cref="IG4PluginManifest"/> based on an array of field names.
            /// </summary>
            /// <param name="expandFields">An array of field names to extract.</param>
            /// <returns>A new <see cref="IG4PluginManifest"/> containing only the specified fields, or the original manifest if no fields are specified.</returns>
            public IG4PluginManifest ExtractFields(params string[] expandFields)
            {
                // Check if no fields are specified, return the original manifest.
                if (expandFields == null || expandFields.Length == 0)
                {
                    return manifest;
                }

                // Define binding flags to search for public instance properties, ignoring case sensitivity.
                const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
                var result = new Dictionary<string, object>();

                // Iterate through each specified field name.
                foreach (var field in expandFields)
                {
                    // Get the property info for the field using reflection.
                    var property = typeof(IG4PluginManifest).GetProperty(field, bindingFlags);

                    // If the property exists, retrieve its value from the manifest and add it to the result dictionary.
                    if (property != null)
                    {
                        var value = property.GetValue(manifest);
                        result.Add(property.Name, value);
                    }
                }

                // If no fields were successfully extracted, return the original manifest.
                if (result.Count == 0)
                {
                    return manifest;
                }

                // Serialize the result dictionary and deserialize it back into a new IG4PluginManifest object.
                return JsonSerializer.Deserialize<G4PluginAttribute>(JsonSerializer.Serialize(result, s_jsonOptions));
            }
        }

        extension(JsonOptions option)
        {
            /// <summary>
            /// Copies the relevant serializer settings from the supplied
            /// <see cref="JsonSerializerOptions"/> into the current <see cref="JsonOptions"/> instance.
            /// </summary>
            /// <param name="serializerOptions">The serializer options whose settings and converters will be applied to the wrapped <see cref="JsonOptions"/> instance.</param>
            /// <returns>The current <see cref="JsonOptions"/> instance after applying the supplied settings.</returns>
            /// <remarks>
            /// This method updates the underlying <c>JsonSerializerOptions</c> instance by
            /// copying the configured ignore behavior, naming policy, encoder,
            /// case-insensitive matching setting, and registered converters.
            /// </remarks>
            public JsonOptions New(JsonSerializerOptions serializerOptions)
            {
                // Disable indentation so serialized JSON stays compact.
                option.JsonSerializerOptions.WriteIndented = false;

                // Copy the null-handling behavior from the supplied serializer options.
                option.JsonSerializerOptions.DefaultIgnoreCondition = serializerOptions.DefaultIgnoreCondition;

                // Copy the property naming policy, such as camelCase.
                option.JsonSerializerOptions.PropertyNamingPolicy = serializerOptions.PropertyNamingPolicy;

                // Copy the encoder configuration used during JSON serialization.
                option.JsonSerializerOptions.Encoder = serializerOptions.Encoder;

                // Copy the case-insensitive property matching behavior used during deserialization.
                option.JsonSerializerOptions.PropertyNameCaseInsensitive = serializerOptions.PropertyNameCaseInsensitive;

                // Copy all registered converters from the supplied serializer options.
                foreach (var converter in serializerOptions.Converters)
                {
                    option.JsonSerializerOptions.Converters.Add(converter);
                }

                // Return the updated JsonOptions instance.
                return option;
            }
        }

        extension(object instance)
        {
            /// <summary>
            /// Confirms the validity of the specified object instance by performing validation.
            /// </summary>
            /// <returns>A tuple containing:
            /// - <c>Valid</c>: <c>true</c> if the object is valid; otherwise, <c>false</c>.
            /// - <c>Results</c>: A list of validation results detailing any validation errors.
            /// </returns>
            public (bool Valid, List<ValidationResult> Results) Confirm()
            {
                // Initialize a list to hold validation results
                var validationResults = new List<ValidationResult>();

                // Create a validation context for the instance
                var validationContext = new ValidationContext(instance, serviceProvider: null, items: null);

                // Perform validation on the instance, validating all properties
                bool isValid = Validator.TryValidateObject(instance, validationContext, validationResults, validateAllProperties: true);

                // Return the validation status and any validation results
                return (isValid, validationResults);
            }

            /// <summary>
            /// Exports the public instance and static properties of the current object,
            /// excluding properties whose names are supplied in the exclude list.
            /// </summary>
            /// <param name="exclude">The property names to exclude from the export operation.</param>
            /// <returns>A dictionary that contains the exported property names and values. Only properties with non-default values are included.</returns>
            public Dictionary<string, object> ExportProperties(params string[] exclude)
            {
                // Use the default binding flags for exporting public instance and static properties.
                var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;

                // Delegate to the overload that accepts explicit binding flags.
                return instance.ExportProperties(flags, exclude);
            }

            /// <summary>
            /// Exports the properties of the current object that match the supplied binding flags,
            /// excluding properties whose names are supplied in the exclude list.
            /// </summary>
            /// <param name="flags">The binding flags used to select the properties to export.</param>
            /// <param name="exclude">The property names to exclude from the export operation.</param>
            /// <returns>A dictionary that contains the exported property names and values. Only properties with non-null and non-default values are included.</returns>
            public Dictionary<string, object> ExportProperties(BindingFlags flags, params string[] exclude)
            {
                // Read all properties from the current instance type using the supplied binding flags
                // and skip any property explicitly listed in the exclude collection.
                var properties = instance
                    .GetType()
                    .GetProperties(flags)
                    .Where(i => !exclude.Contains(i.Name, StringComparer.OrdinalIgnoreCase));

                // Build the lookup table of default values used to filter out uninitialized properties.
                var defaultValues = GetDefaultValues();

                // Create the result dictionary that will hold the exported property values.
                var propertiesResult = new Dictionary<string, object>();

                // Iterate over each property and apply filtering logic to
                // determine whether it should be included in the export result.
                foreach (var property in properties)
                {
                    // Read the current property value from the wrapped instance.
                    var value = property.GetValue(instance);

                    // Skip properties with null values.
                    if (value == null)
                    {
                        continue;
                    }

                    // Resolve the effective property type by unwrapping nullable types.
                    var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                    // Skip enum properties whose numeric value is zero, which usually represents
                    // the default or uninitialized enum member.
                    if (propertyType.IsEnum)
                    {
                        if (Convert.ToInt64(value) == 0)
                        {
                            continue;
                        }
                    }
                    // Skip value types that match their predefined default values.
                    else if (defaultValues.TryGetValue(propertyType, out var defaultValue))
                    {
                        if (Equals(value, defaultValue))
                        {
                            continue;
                        }
                    }

                    // Add the property to the export result when it passed all filtering checks.
                    propertiesResult[property.Name] = value;
                }

                // Return the exported property dictionary.
                return propertiesResult;

                // Creates a lookup table of common CLR types and their default values.
                static Dictionary<Type, object> GetDefaultValues() => new()
                {
                    [typeof(bool)] = false,
                    [typeof(byte)] = (byte)0,
                    [typeof(sbyte)] = (sbyte)0,
                    [typeof(short)] = (short)0,
                    [typeof(ushort)] = (ushort)0,
                    [typeof(int)] = 0,
                    [typeof(uint)] = 0u,
                    [typeof(long)] = 0L,
                    [typeof(ulong)] = 0UL,
                    [typeof(float)] = 0f,
                    [typeof(double)] = 0d,
                    [typeof(decimal)] = 0m,
                    [typeof(TimeSpan)] = default(TimeSpan),
                    [typeof(DateTime)] = default(DateTime),
                    [typeof(DateTimeOffset)] = default(DateTimeOffset),
                    [typeof(Guid)] = Guid.Empty
                };
            }
        }

        extension(SwaggerGenOptions options)
        {
            /// <summary>
            /// Adds one or more OpenAPI documents to the Swagger generator options.
            /// </summary>
            /// <param name="documents">The documents to add.</param>
            /// <remarks>Delegates the addition logic to <see cref="OpenApiManager.AddDocuments"/>.</remarks>
            public void AddDocuments(params OpenApiManager.DocumentDataModel[] documents)
            {
                // Call OpenApiManager to register the documents with the Swagger generator
                OpenApiManager.AddDocuments(options, documents);
            }
        }

        extension(SwaggerUIOptions options)
        {
            /// <summary>
            /// Adds one or more OpenAPI documents to the Swagger UI endpoints.
            /// </summary>
            /// <param name="documents">The OpenAPI documents to register.</param>
            public void AddEndpoints(params OpenApiManager.DocumentDataModel[] documents)
            {
                // If no documents provided, do nothing
                if (documents == null || documents.Length == 0)
                {
                    return;
                }

                // Iterate over each document and register it in Swagger UI
                foreach (var document in documents)
                {
                    options.SwaggerEndpoint(
                        url: document.Url,
                        name: document.OpenApi.Title
                    );
                }
            }
        }
    }
}
