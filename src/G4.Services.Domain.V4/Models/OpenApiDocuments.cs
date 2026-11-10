using G4.Extensions;
using G4.Models;
using G4.Services.Domain.V4.Extensions;
using G4.Services.Domain.V4.Repositories;

using Microsoft.OpenApi;

using System;
using System.Collections.Generic;
using System.Linq;

namespace G4.Services.Domain.V4.Models
{
    /// <summary>
    /// Provides preconfigured OpenAPI documents for the G4 platform.
    /// </summary>
    public static class OpenApiDocuments
    {
        #region *** Fields       ***
        // Internal setup for all available documents.
        private static readonly DocumentSetupModel[] _documents =
        [
            new()
            {
                Name = "g4-api",
                Title = "{productTm} Api",
                Url = "g4-api/docs.json",
                Description = "API documentation for the {product} platform, including endpoints for automation, bots, and tools management.",
                Summary = "Comprehensive API documentation for the {product} platform.",
                Tags = []
            },

            new()
            {
                Name = "automation",
                Url = "automation/docs.json",
                Description = "API documentation for the {product} platform's automation features.",
                Summary = "Comprehensive API documentation for the {product} platform's automation capabilities.",
                Tags = ["Automation"]
            },

            new()
            {
                Name = "bots",
                Url = "bots/docs.json",
                Description = "API documentation for the {product} platform's bot management features.",
                Summary = "Comprehensive API documentation for the {product} platform's bot management capabilities.",
                Tags = ["Bots"]
            },

            new()
            {
                Name = "cache",
                Url = "cache/docs.json",
                Description = "API documentation for the {product} platform's cache management features.",
                Summary = "Comprehensive API documentation for the {product} platform's cache management capabilities.",
                Tags = ["Cache"]
            },

            new()
            {
                Name = "documents",
                Url = "documents/docs.json",
                Description = "API documentation for the {product} platform's document management features.",
                Summary = "Comprehensive API documentation for the {product} platform's document management capabilities.",
                Tags = ["Documents"]
            },

            new()
            {
                Name = "environments",
                Url = "environments/docs.json",
                Description = "API documentation for the {product} platform's environment management features.",
                Summary = "Comprehensive API documentation for the {product} platform's environment management capabilities.",
                Tags = ["Environments"]
            },

            new()
            {
                Name = "files",
                Url = "files/docs.json",
                Description = "API documentation for the {product} platform's file management features.",
                Summary = "Comprehensive API documentation for the {product} platform's file management capabilities.",
                Tags = ["Files"]
            },

            new()
            {
                Name = "flows",
                Url = "flows/docs.json",
                Description = "API documentation for the {product} platform's flow management features.",
                Summary = "Comprehensive API documentation for the {product} platform's flow management capabilities.",
                Tags = ["Flows"]
            },

            new()
            {
                Name = "manifests",
                Url = "manifests/docs.json",
                Description = "API documentation for the {product} platform's manifest management features.",
                Summary = "Comprehensive API documentation for the {product} platform's manifest management capabilities.",
                Tags = ["Manifests"]
            },

            new()
            {
                Name = "mcp",
                Url = "mcp/docs.json",
                Description = "API documentation for the {product} platform's MCP management features.",
                Summary = "Comprehensive API documentation for the {product} platform's MCP management capabilities.",
                Tags = ["Mcp"]
            },

            new()
            {
                Name = "openai",
                Url = "openai/docs.json",
                Description = "API documentation for the {product} platform's OpenAI-compatible proxy features.",
                Summary = "Comprehensive API documentation for the {product} platform's OpenAI-compatible proxy capabilities.",
                Tags = ["OpenAi"]
            },

            new()
            {
                Name = "recordings",
                Url = "recordings/docs.json",
                Description = "API documentation for the {product} platform's recording features.",
                Summary = "Comprehensive API documentation for the {product} platform's recording capabilities.",
                Tags = ["Recordings"]
            },

            new()
            {
                Name = "templates",
                Url = "templates/docs.json",
                Description = "API documentation for the {product} platform's template management features.",
                Summary = "Comprehensive API documentation for the {product} platform's template management capabilities.",
                Tags = ["Templates"]
            }
        ];

        /// <summary>
        /// Preinitialized OpenAPI documents ready for registration.
        /// </summary>
        public static readonly Dictionary<string, OpenApiManager.DocumentDataModel> Documents = Initialize();
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Applies the white-label branding to the title, description, summary, contact, and terms of every document.
        /// </summary>
        /// <param name="branding">The resolved branding settings.</param>
        /// <remarks>
        /// The Hub calls this once at startup, before the Swagger UI reads the document titles for its document
        /// list. The documents are shared static state, so applying the branding again resolves no further
        /// placeholders and only rewrites the contact and the terms with the same values.
        /// </remarks>
        public static void SetBranding(BrandingSettingsModel branding)
        {
            // Require the branding, so a wiring mistake fails at startup instead of publishing raw placeholders.
            ArgumentNullException.ThrowIfNull(argument: branding);

            foreach (var document in Documents.Values)
            {
                // Resolve the placeholders in the text the Swagger UI shows for the document.
                var info = document.OpenApi;
                info.Description = branding.ResolveText(info.Description);
                info.Summary = branding.ResolveText(info.Summary);
                info.Title = branding.ResolveText(info.Title);

                // Publish the configured support contact and terms instead of the G4 defaults.
                info.Contact = new OpenApiContact
                {
                    Email = branding.Support.Email,
                    Name = branding.Support.Name,
                    Url = new Uri(branding.Support.Url)
                };
                info.TermsOfService = new Uri(branding.TermsUrl);
            }
        }

        /// <summary>
        /// Initializes all document data models and enriches them with G4 metadata.
        /// </summary>
        /// <returns>An array of <see cref="DocumentDataModel"/> objects.</returns>
        private static Dictionary<string, OpenApiManager.DocumentDataModel> Initialize()
        {
            // Create a list to hold the initialized documents
            var documents = new List<OpenApiManager.DocumentDataModel>();

            // Iterate over each document setup model and create the corresponding DocumentDataModel
            foreach (var document in _documents)
            {
                // Create a new DocumentDataModel from the setup model
                var documentDataModel = OpenApiManager.NewDocument(
                    document.Name,
                    document.Url,
                    document.Description,
                    document.Summary);

                // Assign tags from the setup model to the DocumentDataModel
                documentDataModel.Tags = document.Tags;

                // Replace the name-derived title when the setup names the product, so the title carries a branding
                // placeholder that SetBranding resolves.
                if (!string.IsNullOrEmpty(document.Title))
                {
                    documentDataModel.OpenApi.Title = document.Title;
                }

                // Add G4-specific OpenAPI metadata (contact, license, terms); SetBranding replaces the contact and
                // the terms with the configured branding.
                documentDataModel
                    .OpenApi
                    .AddG4Contact()
                    .AddG4License()
                    .AddG4TermsOfService();

                // Add to the result list
                documents.Add(documentDataModel);
            }

            // return a dictionary keyed by document name for easy access
            return documents.ToDictionary(
                keySelector: item => item.Name,
                elementSelector: item => item,
                comparer: StringComparer.OrdinalIgnoreCase);
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents the internal setup information for a document.
        /// </summary>
        private sealed class DocumentSetupModel
        {
            /// <summary>
            /// Short description of the API document.
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// Name of the document (used as identifier).
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Summary of the document for display purposes.
            /// </summary>
            public string Summary { get; set; }

            /// <summary>
            /// The tags associated with the API document, used for categorization in Swagger UI.
            /// </summary>
            public string[] Tags { get; set; }

            /// <summary>
            /// The document title, which may hold branding placeholders.
            /// </summary>
            /// <remarks>
            /// Null keeps the title derived from <see cref="Name"/>; a value replaces it, for example when the title
            /// names the product.
            /// </remarks>
            public string Title { get; set; }

            /// <summary>
            /// URL path to the Swagger/OpenAPI JSON file.
            /// </summary>
            public string Url { get; set; }
        }
        #endregion
    }
}
