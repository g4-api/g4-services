using G4.Extensions;
using G4.Settings;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;

using System;
using System.Collections.Generic;
using System.Linq;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Provides helper operations for creating, organizing, and registering
    /// OpenAPI document definitions used by the application.
    /// </summary>
    /// <remarks>
    /// This manager centralizes the logic for building Swagger/OpenAPI document
    /// metadata, applying document filters, and registering documents in the
    /// Swagger generation pipeline.
    /// </remarks>
    public sealed class OpenApiManager
    {
        #region *** Methods      ***
        /// <summary>
        /// Adds one or more Swagger documents to the Swagger generation options.
        /// </summary>
        /// <param name="options">The Swagger generation options to update.</param>
        /// <param name="documents">The Swagger document definitions to register.</param>
        /// <remarks>This method ignores the call when the supplied document collection is <c>null</c> or empty.</remarks>
        public static void AddDocuments(SwaggerGenOptions options, params DocumentDataModel[] documents)
        {
            // Ignore the request when no Swagger documents were supplied.
            if (documents == null || documents.Length == 0)
            {
                return;
            }

            // Register each supplied Swagger document in the generator options.
            foreach (var document in documents)
            {
                options.SwaggerDoc(name: document.Name, info: document.OpenApi);
            }
        }

        /// <summary>
        /// Creates a Swagger document definition from the supplied metadata.
        /// </summary>
        /// <param name="name">The internal document name used to register the Swagger document.</param>
        /// <param name="url">The relative or absolute URL where the document is exposed.</param>
        /// <param name="description">The descriptive text shown for the Swagger document.</param>
        /// <param name="summary">The short summary shown for the Swagger document.</param>
        /// <returns>A <see cref="DocumentDataModel"/> populated with the generated OpenAPI metadata.</returns>
        /// <remarks>
        /// The document title is derived from the document name by normalizing separators
        /// and converting the value into a readable space-separated title.
        /// </remarks>
        public static DocumentDataModel NewDocument(
            string name,
            string url,
            string description,
            string summary)
        {
            // Build a readable document title from the internal document name.
            var title = name
                    .Replace("-", " ")
                    .ConvertToPascalCase()
                    .ConvertToSpaceCase();

            // Create the OpenAPI metadata object used by the Swagger document.
            var info = new OpenApiInfo()
            {
                Description = description,
                Summary = summary,
                Title = title,
                Version = $"v{AppSettings.ApiVersion}"
            };

            // Return the final document model with its registration name,
            // OpenAPI metadata, and exposed URL.
            return new()
            {
                Name = name,
                OpenApi = info,
                Url = url
            };
        }

        /// <summary>
        /// Determines whether the specified API description contains at least one
        /// Swagger operation tag that matches any of the supplied tags.
        /// </summary>
        /// <param name="description">The API description to evaluate.</param>
        /// <param name="tags">The tags to match against the Swagger operation metadata.</param>
        /// <returns><c>true</c> when no tags were supplied, or when at least one Swagger operation tag matches one of the provided tags; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Tag comparison is performed using case-insensitive matching.
        /// Only <see cref="SwaggerOperationAttribute"/> metadata entries are inspected.
        /// </remarks>
        public static bool AssertDescription(ApiDescription description, params string[] tags)
        {
            // When no tags are supplied, treat the description as a match.
            if (tags == null || tags.Length == 0)
            {
                return true;
            }

            // Normalize the input tags into a case-insensitive set for efficient matching.
            var tagSet = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);

            // Check whether any Swagger operation attribute on the endpoint metadata
            // contains at least one tag that matches the supplied tag set.
            return description
                .ActionDescriptor
                .EndpointMetadata
                .OfType<SwaggerOperationAttribute>()
                .Any(attribute => attribute.Tags != null && attribute.Tags.Any(tag => tagSet.Contains(tag)));
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents the metadata required to register and describe a Swagger document.
        /// </summary>
        /// <remarks>
        /// This model contains the OpenAPI metadata, internal document name,
        /// optional tag filters, and the URL where the document is exposed.
        /// </remarks>
        public sealed class DocumentDataModel
        {
            /// <summary>
            /// Gets or sets the OpenAPI metadata associated with the document.
            /// </summary>
            public OpenApiInfo OpenApi { get; set; }

            /// <summary>
            /// Gets or sets the internal name used to register the document.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the optional tag filters associated with the document.
            /// </summary>
            /// <remarks>
            /// These tags can be used to associate API operations with this document.
            /// </remarks>
            public string[] Tags { get; set; }

            /// <summary>
            /// Gets or sets the URL where the document is exposed.
            /// </summary>
            public string Url { get; set; }
        }
        #endregion
    }
}
