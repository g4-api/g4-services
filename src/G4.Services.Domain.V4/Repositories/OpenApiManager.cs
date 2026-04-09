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
    public sealed class OpenApiManager
    {
        public static void AddDocuments(SwaggerGenOptions options, params DocumentDataModel[] documents)
        {
            if(documents == null || documents.Length == 0)
            {
                return;
            }

            foreach (var document in documents)
            {
                options.SwaggerDoc(name: document.Name, info: document.OpenApi);
            }
        }

        public static DocumentDataModel NewDocument(
            string name,
            string url,
            string description,
            string summary)
        {
            var title = name
                    .Replace("-", " ")
                    .ConvertToPascalCase()
                    .ConvertToSpaceCase();

            var info = new OpenApiInfo()
            {
                Description = description,
                Summary = summary,
                Title = title,
                Version = $"v{AppSettings.ApiVersion}"
            };

            return new()
            {
                Name = name,
                OpenApi = info,
                Url = url
            };
        }

        public static bool AssertDescription(ApiDescription description, params string[] tags)
        {
            if (tags == null || tags.Length == 0)
            {
                return true;
            }

            // Normalize input tags for case-insensitive comparison
            var tagSet = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);

            return description
                .ActionDescriptor
                .EndpointMetadata
                .OfType<SwaggerOperationAttribute>()
                .Any(attribute => attribute.Tags != null && attribute.Tags.Any(tag => tagSet.Contains(tag)));
        }

        public sealed class DocumentDataModel
        {
            public OpenApiInfo OpenApi { get; set; }
            public string Name { get; set; }
            public string[] Tags { get; set; }
            public string Url { get; set; }
        }
    }
}
