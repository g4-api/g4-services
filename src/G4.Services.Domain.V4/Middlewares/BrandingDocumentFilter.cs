using G4.Models;
using G4.Services.Domain.V4.Extensions;

using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

using System.Collections.Generic;
using System.Linq;

namespace G4.Services.Domain.V4.Middlewares
{
    /// <summary>
    /// Replaces the branding placeholders in every visible text of a generated Swagger document.
    /// </summary>
    /// <remarks>
    /// The API documentation text names the product through placeholders such as <c>{productTm}</c>, because
    /// attribute text is a compile-time constant. This filter runs after the document is generated and resolves the
    /// placeholders in the document information, the tags, the operations, their parameters, request bodies, and
    /// responses, and the component schemas. Identifiers and routes contain no placeholders, so they never change.
    /// </remarks>
    /// <param name="branding">The resolved branding that holds the placeholder values.</param>
    public sealed class BrandingDocumentFilter(BrandingSettingsModel branding) : IDocumentFilter
    {
        #region *** Fields       ***

        // The resolved branding the Hub registered at startup; it holds the value of every placeholder.
        private readonly BrandingSettingsModel _branding = branding;

        #endregion

        #region *** Methods      ***

        /// <inheritdoc />
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            // Resolve the document information, which the Swagger UI shows in its page header.
            if (swaggerDoc.Info is not null)
            {
                swaggerDoc.Info.Description = _branding.ResolveText(swaggerDoc.Info.Description);
                swaggerDoc.Info.Summary = _branding.ResolveText(swaggerDoc.Info.Summary);
                swaggerDoc.Info.Title = _branding.ResolveText(swaggerDoc.Info.Title);
            }

            // Resolve the tag descriptions, which head each controller group.
            foreach (var tag in swaggerDoc.Tags?.AsEnumerable() ?? [])
            {
                tag.Description = _branding.ResolveText(tag.Description);
            }

            // Resolve every operation with its parameters, request body, and responses.
            foreach (var pathItem in swaggerDoc.Paths?.Values.AsEnumerable() ?? [])
            {
                foreach (var operation in pathItem.Operations?.Values.AsEnumerable() ?? [])
                {
                    ResolveOperation(_branding, operation);
                }
            }

            // Resolve the component schemas once each, since nested schemas can refer back to each other.
            var visitedSchemas = new HashSet<IOpenApiSchema>(ReferenceEqualityComparer.Instance);

            foreach (var schema in swaggerDoc.Components?.Schemas?.Values ?? [])
            {
                ResolveSchema(_branding, schema, visitedSchemas);
            }
        }

        // Resolves the summary, the description, the parameters, the request body, and the responses of one
        // operation. Referenced parameters, bodies, and responses are skipped, because their component carries the
        // text and is resolved with the components.
        private static void ResolveOperation(BrandingSettingsModel branding, OpenApiOperation operation)
        {
            // Resolve the operation text shown in the operation header.
            operation.Description = branding.ResolveText(operation.Description);
            operation.Summary = branding.ResolveText(operation.Summary);

            // Resolve each inline parameter description.
            foreach (var parameter in operation.Parameters ?? [])
            {
                if (parameter is OpenApiParameter inlineParameter)
                {
                    inlineParameter.Description = branding.ResolveText(inlineParameter.Description);
                }
            }

            // Resolve the inline request body description.
            if (operation.RequestBody is OpenApiRequestBody requestBody)
            {
                requestBody.Description = branding.ResolveText(requestBody.Description);
            }

            // Resolve each inline response description.
            foreach (var response in operation.Responses?.Values.AsEnumerable() ?? [])
            {
                if (response is OpenApiResponse inlineResponse)
                {
                    inlineResponse.Description = branding.ResolveText(inlineResponse.Description);
                }
            }
        }

        // Resolves the description of one schema and of its properties, items, and composed schemas. The visited set
        // stops recursive schemas from being walked twice.
        private static void ResolveSchema(BrandingSettingsModel branding, IOpenApiSchema schema, HashSet<IOpenApiSchema> visitedSchemas)
        {
            // Skip references, since their component schema carries the text and is resolved with the components.
            if (schema is not OpenApiSchema inlineSchema)
            {
                return;
            }

            // Skip a schema already resolved, so each schema is resolved once and recursive schemas end.
            if (!visitedSchemas.Add(inlineSchema))
            {
                return;
            }

            // Resolve the schema description shown next to the model.
            inlineSchema.Description = branding.ResolveText(inlineSchema.Description);

            // Collect the nested schemas whose descriptions are shown with this model.
            var nestedSchemas = new List<IOpenApiSchema>();
            nestedSchemas.AddRange(inlineSchema.Properties?.Values ?? []);
            nestedSchemas.AddRange(inlineSchema.AllOf ?? []);
            nestedSchemas.AddRange(inlineSchema.AnyOf ?? []);
            nestedSchemas.AddRange(inlineSchema.OneOf ?? []);

            if (inlineSchema.Items is not null)
            {
                nestedSchemas.Add(inlineSchema.Items);
            }

            // Resolve each nested schema with the same visited set.
            foreach (var nestedSchema in nestedSchemas)
            {
                ResolveSchema(branding, nestedSchema, visitedSchemas);
            }
        }

        #endregion
    }
}
