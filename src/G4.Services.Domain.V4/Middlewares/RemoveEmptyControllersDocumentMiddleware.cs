using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

using System;
using System.Collections.Generic;
using System.Linq;

namespace G4.Services.Domain.V4.Middlewares
{
    /// <summary>
    /// Middleware for filtering out empty controllers from the Swagger API documentation.
    /// This ensures that controllers without any defined actions are not shown in the generated Swagger docs.
    /// </summary>
    public class RemoveEmptyControllersDocumentMiddleware : IDocumentFilter
    {
        /// <inheritdoc />
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            // Collect all tag names that are referenced by any operation in the paths
            var referencedTags = swaggerDoc.Paths
                .SelectMany(p => p.Value.Operations.Values)   // Flatten all operations across all paths
                .SelectMany(op => op.Tags)                    // Flatten the tags within each operation
                .Select(t => t.Name)                          // Extract the tag names
                .ToHashSet(StringComparer.OrdinalIgnoreCase); // Use a HashSet for unique tags with case-insensitive comparison

            // Filter the tags in the Swagger document to include only those that are referenced by operations
            var collection = swaggerDoc.Tags.Where(t => referencedTags.Contains(t.Name));

            // Replace the existing tags in the Swagger document with the filtered set
            swaggerDoc.Tags = new HashSet<OpenApiTag>(collection);
        }
    }
}
