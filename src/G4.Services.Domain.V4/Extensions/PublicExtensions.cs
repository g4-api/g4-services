using G4.Attributes;
using G4.Models;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json;

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

        /// <summary>
        /// Extracts specific fields from the <see cref="IG4PluginManifest"/> based on a comma-separated list of field names.
        /// </summary>
        /// <param name="manifest">The plugin manifest from which to extract fields.</param>
        /// <param name="expandFields">A comma-separated list of field names to extract.</param>
        /// <returns>A new <see cref="IG4PluginManifest"/> containing only the specified fields, or the original manifest if no fields are specified.</returns>
        public static IG4PluginManifest ExtractFields(this IG4PluginManifest manifest, string expandFields)
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
        /// <param name="manifest">The plugin manifest from which to extract fields.</param>
        /// <param name="expandFields">An array of field names to extract.</param>
        /// <returns>A new <see cref="IG4PluginManifest"/> containing only the specified fields, or the original manifest if no fields are specified.</returns>
        public static IG4PluginManifest ExtractFields(this IG4PluginManifest manifest, params string[] expandFields)
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

        /// <summary>
        /// Confirms the validity of the specified object instance by performing validation.
        /// </summary>
        /// <param name="instance">The object instance to validate.</param>
        /// <returns>A tuple containing:
        /// - <c>Valid</c>: <c>true</c> if the object is valid; otherwise, <c>false</c>.
        /// - <c>Results</c>: A list of validation results detailing any validation errors.
        /// </returns>
        public static (bool Valid, List<ValidationResult> Results) Confirm(this object instance)
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
    }
}
