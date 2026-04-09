using G4.Extensions;
using G4.Services.Domain.V4.Repositories;

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
                Url = "g4-api/docs.json",
                Description = "API documentation for the G4 platform, including endpoints for automation, bots, and tools management.",
                Summary = "Comprehensive API documentation for the G4 platform.",
                Tags = []
            },

            new()
            {
                Name = "bots",
                Url = "bots/docs.json",
                Description = "API documentation for the G4 platform's bot management features.",
                Summary = "Comprehensive API documentation for the G4 platform's bot management capabilities.",
                Tags = ["Bots"]
            },

            new()
            {
                Name = "cache",
                Url = "cache/docs.json",
                Description = "API documentation for the G4 platform's cache management features.",
                Summary = "Comprehensive API documentation for the G4 platform's cache management capabilities.",
                Tags = ["Cache"]
            },

            new()
            {
                Name = "environments",
                Url = "environments/docs.json",
                Description = "API documentation for the G4 platform's environment management features.",
                Summary = "Comprehensive API documentation for the G4 platform's environment management capabilities.",
                Tags = ["Environments"]
            },

            new()
            {
                Name = "openai-tools",
                Url = "openai-tools/docs.json",
                Description = "API documentation for the G4 platform's OpenAI tools management features.",
                Summary = "Comprehensive API documentation for the G4 platform's OpenAI tools management capabilities.",
                Tags = ["AiTools"]
            }
        ];

        /// <summary>
        /// Preinitialized OpenAPI documents ready for registration.
        /// </summary>
        public static readonly Dictionary<string, OpenApiManager.DocumentDataModel> Documents = Initialize();
        #endregion

        #region *** Methods      ***
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

                // Add G4-specific OpenAPI metadata (contact, license, terms, trademark)
                documentDataModel
                    .OpenApi
                    .AddG4Contact()
                    .AddG4License()
                    .AddG4TermsOfService()
                    .AddG4Trademark();

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
            /// URL path to the Swagger/OpenAPI JSON file.
            /// </summary>
            public string Url { get; set; }
        }
        #endregion
    }
}
