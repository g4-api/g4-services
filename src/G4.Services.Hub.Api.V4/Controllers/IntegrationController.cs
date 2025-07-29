using G4.Extensions;
using G4.Models;
using G4.Services.Domain.V4;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Swashbuckle.AspNetCore.Annotations;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/[controller]")]
    [SwaggerTag(description: "Provides endpoints to retrieve G4™ engine metadata, enabling seamless integration with external client applications.")]
    public class IntegrationController(IDomain domain) : ControllerBase
    {
        // The domain service for the G4™ engine.
        private readonly IDomain _domain = domain;

        [HttpGet]
        [Route("cache")]
        [SwaggerOperation(
            summary: "Retrieves the current plugin cache.",
            description: "Fetches the cached data for plugins, stored in a dictionary format. The cache includes plugin information keyed by plugin type and name, and is returned in JSON format.",
            Tags = ["Integration", "Cache"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully retrieved the plugin cache.", type: typeof(IDictionary<string, ConcurrentDictionary<string, G4PluginCacheModel>>), contentTypes: MediaTypeNames.Application.Json)]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client, NoStore = false)]
        public IActionResult GetCache()
        {
            // Retrieve the plugin cache from the domain's integration layer.
            var cache = _domain.G4.Integration.GetCache();

            // Return the cache as a JSON response.
            return Ok(cache);
        }

        [HttpPost]
        [Route("cache")]
        [SwaggerOperation(
            summary: "Retrieves the plugin cache from specified external repositories.",
            description: "Fetches the cached plugin data from the specified external repositories, returning the data in a dictionary format. The cache is stored in JSON format and is client-cached for 60 seconds.",
            Tags = ["Integration", "Cache"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully retrieved the plugin cache from the specified repositories.", type: typeof(IDictionary<string, ConcurrentDictionary<string, G4PluginCacheModel>>), contentTypes: MediaTypeNames.Application.Json)]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client, NoStore = false)]
        public IActionResult GetCache(
            [SwaggerParameter(description: "The external repositories from which to retrieve the plugin cache.", Required = true)][FromBody] G4ExternalRepositoryModel[] repositories)
        {
            // Retrieve the plugin cache from the specified repositories via the integration layer.
            var cache = _domain.G4.Integration.GetCache(repositories);

            // Return the cache as a JSON response.
            return Ok(cache);
        }

        [HttpGet]
        [Route("cache/sync")]
        [SwaggerOperation(
            summary: "Synchronizes the internal plugin cache.",
            description: "Triggers the synchronization of the internal plugin cache, updating cached data from internal resources and connected libraries within the G4™ engine integration. This operation ensures that the internal cache is aligned with the latest data from internal sources, excluding external repositories.",
            Tags = ["Integration", "Cache"])]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "Successfully synchronized the internal plugin cache. No content is returned.")]
        public IActionResult SyncCache()
        {
            // Synchronize the internal plugin cache using internal resources and connected libraries.
            _domain.G4.Integration.SyncCache(_domain.Cache);

            // Return a 204 No Content response, indicating successful synchronization with no response body.
            return NoContent();
        }

        [HttpPost]
        [Route("cache/sync")]
        [SwaggerOperation(
            summary: "Synchronizes the plugin cache with external repositories.",
            description: "Triggers the synchronization of the plugin cache, updating cached data from the specified external repositories. This operation ensures that the cache is aligned with the latest data from the provided repositories.",
            Tags = ["Integration", "Cache"])]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "Successfully synchronized the plugin cache from the provided external repositories. No content is returned.")]
        public IActionResult SyncCache(
            [SwaggerParameter(description: "An array of external repository details to sync the plugin cache with.", Required = true)][FromBody] G4ExternalRepositoryModel[] repositories)
        {
            // Synchronize the plugin cache using the provided external repositories.
            _domain.G4.Integration.SyncCache(_domain.Cache, repositories);

            // Return a 204 No Content response, indicating successful synchronization with no response body.
            return NoContent();
        }

        [HttpGet]
        [Route("cache/dataset")]
        public IActionResult GetDataSet()
        {
            var d = _domain.Cache.PluginsCache.Values;
            var j = d.SelectMany(i => i.Values).Select(i => new
            {
                source_id = i.Manifest.Key,
                Details = new
                {
                    i.Manifest,
                    i.Document,
                    i.Manifest.Examples
                }
            });


            // Create a memory stream to hold the ZIP archive
            using (var zipStream = new MemoryStream())
            {
                // Create the zip archive
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    foreach (var jsonFile in j)
                    {
                        // Create an entry for each JSON file
                        var entry = archive.CreateEntry($"{jsonFile.source_id}.json", CompressionLevel.Fastest);
                        using (var entryStream = entry.Open())
                        using (var streamWriter = new StreamWriter(entryStream))
                        {
                            var value = JsonSerializer.Serialize(jsonFile);
                            streamWriter.Write(value);
                        }
                    }
                }

                // Reset the stream position to the beginning before returning
                zipStream.Position = 0;

                // Return the zip file as a FileResult
                return File(zipStream.ToArray(), "application/zip", "jsonFiles.zip");
            }
        }

        [HttpGet]
        [Route("documents/key/{key}")]
        [SwaggerOperation(
            summary: "Retrieves plugin Markdown documentation by key.",
            description: "Fetches the plugin Markdown documentation identified by the specified plugin name key. Returns the documentation content in Markdown format if found.",
            Tags = ["Integration", "Documents"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully retrieved the plugin Markdown documentation.", contentTypes: MediaTypeNames.Text.Markdown)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Plugin Markdown documentation not found for the provided plugin name.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        public IActionResult GetDocument(
            [SwaggerParameter(description: "The name of the plugin identifying the Markdown documentation to retrieve.", Required = true)] string key)
        {
            // Attempt to retrieve the Markdown documentation using the provided plugin name.
            var document = _domain.G4.Integration.GetDocument(key);

            if (!string.IsNullOrEmpty(document))
            {
                // Return the Markdown documentation content if found.
                return Content(document, MediaTypeNames.Text.Markdown);
            }

            // Create an error model indicating the documentation was not found.
            var error404 = new GenericErrorModel(HttpContext)
                .AddError(nameof(key), $"Plugin Markdown documentation with plugin name '{key}' not found.");

            // Return a 404 Not Found response with the error model.
            return NotFound(error404);
        }

        [HttpGet]
        [Route("documents/type/{pluginType}/key/{key}")]
        [SwaggerOperation(
            summary: "Retrieves plugin Markdown documentation by plugin type and key.",
            description: "Fetches the plugin Markdown documentation identified by the specified plugin type and name key. Returns the documentation content in Markdown format if found.",
            Tags = ["Integration", "Documents"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully retrieved the plugin Markdown documentation.", contentTypes: MediaTypeNames.Text.Markdown)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Plugin Markdown documentation not found for the provided plugin type and name.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        public IActionResult GetDocument(
            [SwaggerParameter(description: "The type/category of the plugin identifying the Markdown documentation to retrieve.", Required = true)] string pluginType,
            [SwaggerParameter(description: "The name of the plugin identifying the Markdown documentation to retrieve.", Required = true)] string key)
        {
            // Attempt to retrieve the Markdown documentation using the provided plugin type and name.
            var document = _domain.G4.Integration.GetDocument(pluginType, key);

            if (!string.IsNullOrEmpty(document))
            {
                // Return the Markdown documentation content if found.
                return Content(document, MediaTypeNames.Text.Markdown);
            }

            // Create an error model indicating the documentation was not found.
            var error404 = new GenericErrorModel(HttpContext)
                .AddError(nameof(key), $"Plugin Markdown documentation with plugin name '{key}' not found.")
                .AddError(nameof(pluginType), $"Plugin type '{pluginType}' does not have Markdown documentation with the name '{key}'.");

            // Return a 404 Not Found response with the error model.
            return NotFound(error404);
        }

        [HttpPost]
        [Route("documents/key/{key}")]
        [Consumes(contentType: MediaTypeNames.Application.Json)]
        [SwaggerOperation(
            summary: "Retrieves plugin Markdown documentation by key and repository.",
            description: "Fetches the plugin Markdown documentation identified by the specified plugin name key and external repository information. Returns the documentation content in Markdown format if found.",
            Tags = ["Integration", "Documents"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully retrieved the plugin Markdown documentation.", type: typeof(string), contentTypes: MediaTypeNames.Text.Markdown)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Plugin Markdown documentation not found for the provided plugin name and repository.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        public IActionResult GetDocument(
            [SwaggerParameter(description: "The name of the plugin identifying the Markdown documentation to retrieve.", Required = true)] string key,
            [SwaggerParameter(description: "The external repository information where the plugin is located.", Required = true)][FromBody] G4ExternalRepositoryModel repository)
        {
            // Attempt to retrieve the Markdown documentation using the provided plugin name and repository.
            var document = _domain.G4.Integration.GetDocument(key, repository);

            if (!string.IsNullOrEmpty(document))
            {
                // Return the Markdown documentation content if found.
                return Content(document, MediaTypeNames.Text.Markdown);
            }

            // Create an error model indicating the documentation was not found.
            var error404 = new GenericErrorModel(HttpContext)
                .AddError(nameof(key), $"Plugin Markdown documentation with plugin name '{key}' not found on repository {repository.Url}.")
                .SetRequest(request: repository);

            // Return a 404 Not Found response with the error model.
            return NotFound(error404);
        }

        [HttpPost]
        [Route("documents/type/{pluginType}/key/{key}")]
        [Consumes(contentType: MediaTypeNames.Application.Json)]
        [SwaggerOperation(
            summary: "Retrieves plugin Markdown documentation by plugin type, key, and repository.",
            description: "Fetches the plugin Markdown documentation identified by the specified plugin type, name key, and external repository information. Returns the documentation content in Markdown format if found.",
            Tags = ["Integration", "Documents"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully retrieved the plugin Markdown documentation.", type: typeof(string), contentTypes: MediaTypeNames.Text.Markdown)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Plugin Markdown documentation not found for the provided plugin type, name, or repository.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        public IActionResult GetDocument(
            [SwaggerParameter(description: "The type/category of the plugin identifying the Markdown documentation to retrieve.", Required = true)] string pluginType,
            [SwaggerParameter(description: "The name of the plugin identifying the Markdown documentation to retrieve.", Required = true)] string key,
            [SwaggerParameter(description: "The external repository information where the plugin is located.", Required = true)][FromBody] G4ExternalRepositoryModel repository)
        {
            // Attempt to retrieve the Markdown documentation using the provided plugin type, name, and repository.
            var document = _domain.G4.Integration.GetDocument(pluginType, key, repository);

            // If the document is found, return the content as Markdown.
            if (!string.IsNullOrEmpty(document))
            {
                return Content(document, MediaTypeNames.Text.Markdown);
            }

            // Create an error model indicating that the documentation was not found.
            var error404 = new GenericErrorModel(HttpContext)
                .AddError(nameof(key), $"Plugin Markdown documentation with plugin name '{key}' not found on repository '{repository.Url}' for plugin type '{pluginType}'.")
                .SetRequest(request: repository);

            // Return a 404 Not Found response with the error model.
            return NotFound(error404);
        }

        [HttpGet]
        [Route("files")]
        [SwaggerOperation(
            summary: "Lists all static files in wwwroot.",
            description: "Recursively scans the wwwroot directory and returns a list of all static file paths, relative to wwwroot. Useful for discovering available static resources such as HTML, JS, CSS, images, etc.",
            Tags = new[] { "Integration", "Files" })]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully returned a list of all static files found under wwwroot.", type: typeof(List<string>), contentTypes: MediaTypeNames.Application.Json)]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client, NoStore = false)]
        public IActionResult GetStaticFilesList()
        {
            // Resolve the absolute path to wwwroot.
            var wwwrootPath = Path.Combine(_domain.Environment.ContentRootPath, "wwwroot");

            // If wwwroot does not exist, return an empty list.
            if (!Directory.Exists(wwwrootPath))
            {
                return Ok(new List<string>());
            }

            // Enumerate all files recursively under wwwroot, returning paths relative to wwwroot (using forward slashes for URL compatibility).
            var files = Directory.EnumerateFiles(wwwrootPath, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(wwwrootPath, f).Replace("\\", "/"))
                .ToList();

            // Return the list of relative file paths as JSON.
            return Ok(files);
        }

        [HttpGet]
        [Route("manifests/key/{key}")]
        [SwaggerOperation(
            summary: "Retrieves the plugin manifest by key.",
            description: "Fetches the plugin manifest, including optional Markdown documentation, identified by the specified plugin name key. Optionally includes specified fields in the response. Returns the manifest content in JSON format if found.",
            Tags = ["Integration", "Manifests"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully retrieved the plugin manifest.", type: typeof(IG4PluginManifest), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Plugin manifest not found for the provided plugin name.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        public IActionResult GetManifest(
            [SwaggerParameter(description: "The name of the plugin identifying the manifest to retrieve.", Required = true)] string key,
            [SwaggerParameter(description: "Comma-separated list of fields to include in the response.", Required = false)][FromQuery] string expandFields)
        {
            // Attempt to retrieve the plugin manifest using the provided key.
            var manifest = _domain.G4.Integration.GetManifest<IG4PluginManifest>(key);

            // If the manifest is found, return it in JSON format.
            // If 'expandFields' is provided, return only the specified fields from the manifest.
            if (manifest != default)
            {
                return Ok(manifest.ExtractFields(expandFields));
            }

            // Create an error model indicating that the manifest was not found.
            var error404 = new GenericErrorModel(HttpContext)
                .AddError(nameof(key), $"Plugin manifest with plugin name '{key}' not found.");

            // Return a 404 Not Found response with the error model.
            return NotFound(error404);
        }

        [HttpGet]
        [Route("manifests/type/{pluginType}/key/{key}")]
        [SwaggerOperation(
            summary: "Retrieves plugin manifest by plugin type and key.",
            description: "Fetches the plugin manifest identified by the specified plugin type and name key. Optionally includes specified fields in the response. Returns the manifest data in JSON format if found.",
            Tags = ["Integration", "Manifests"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully retrieved the plugin manifest.", type: typeof(IG4PluginManifest), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Plugin manifest not found for the provided plugin type and name.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        public IActionResult GetManifest(
            [SwaggerParameter(description: "The type/category of the plugin identifying the manifest to retrieve.", Required = true)] string pluginType,
            [SwaggerParameter(description: "The name of the plugin identifying the manifest to retrieve.", Required = true)] string key,
            [SwaggerParameter(description: "Comma-separated list of fields to include in the response.", Required = false)][FromQuery] string expandFields)
        {
            // Attempt to retrieve the manifest using the provided plugin type and key.
            var manifest = _domain.G4.Integration.GetManifest<IG4PluginManifest>(pluginType, key);

            // If the manifest is found, return it in JSON format.
            // If 'expandFields' is provided, return only the specified fields.
            if (manifest != default)
            {
                return Ok(manifest.ExtractFields(expandFields));
            }

            // Create an error model indicating the manifest was not found.
            var error404 = new GenericErrorModel(HttpContext)
                .AddError(nameof(key), $"Plugin manifest with key '{key}' not found for plugin type '{pluginType}'.");

            // Return a 404 Not Found response with the error model.
            return NotFound(error404);
        }

        [HttpPost]
        [Route("manifests/key/{key}")]
        [Consumes(contentType: MediaTypeNames.Application.Json)]
        [SwaggerOperation(
            summary: "Retrieves plugin manifest by key and repository.",
            description: "Fetches the plugin manifest identified by the specified plugin name key and external repository information. Optionally includes specified fields in the response. Returns the manifest data in JSON format if found.",
            Tags = ["Integration", "Manifests"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully retrieved the plugin manifest.", type: typeof(IG4PluginManifest), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Plugin manifest not found for the provided plugin name and repository.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        public IActionResult GetManifest(
            [SwaggerParameter(description: "The name of the plugin identifying the manifest to retrieve.", Required = true)] string key,
            [SwaggerParameter(description: "Comma-separated list of fields to include in the response.", Required = false)][FromQuery] string expandFields,
            [SwaggerParameter(description: "The external repository information where the plugin is located.", Required = true)][FromBody] G4ExternalRepositoryModel repository)
        {
            // Attempt to retrieve the manifest using the provided plugin name and repository.
            var manifest = _domain.G4.Integration.GetManifest<IG4PluginManifest>(key, repository);

            // If the manifest is found, return it as a JSON result.
            // If 'expandFields' is provided, extract only the specified fields from the manifest.
            if (manifest != default)
            {
                return Ok(manifest.ExtractFields(expandFields));
            }

            // Create an error model indicating the manifest was not found.
            var error404 = new GenericErrorModel(HttpContext)
                .AddError(nameof(key), $"Plugin manifest with key '{key}' not found on repository '{repository.Url}'.") // Clear error message
                .SetRequest(request: repository);

            // Return a 404 Not Found response with the error model.
            return NotFound(error404);
        }

        [HttpPost]
        [Route("manifests/type/{pluginType}/key/{key}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [SwaggerOperation(
            summary: "Retrieves plugin manifest by plugin type, key, and repository.",
            description: "Fetches the plugin manifest identified by the specified plugin type, name key, and external repository information. Returns the manifest data in JSON format if found.",
            Tags = ["Integration", "Manifests"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully retrieved the plugin manifest.", type: typeof(IG4PluginManifest), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Plugin manifest not found for the provided plugin type, name, or repository.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        public IActionResult GetManifest(
            [SwaggerParameter(description: "The type/category of the plugin identifying the manifest to retrieve.", Required = true)] string pluginType,
            [SwaggerParameter(description: "The name of the plugin identifying the manifest to retrieve.", Required = true)] string key,
            [SwaggerParameter(description: "Comma-separated list of fields to include in the response.", Required = false)][FromQuery] string expandFields,
            [SwaggerParameter(description: "The external repository information where the plugin is located.", Required = true)][FromBody] G4ExternalRepositoryModel repository)
        {
            // Attempt to retrieve the manifest using the provided plugin type, name, and repository.
            var manifest = _domain.G4.Integration.GetManifest<IG4PluginManifest>(pluginType, key, repository);

            // If the manifest is found, return it as a JSON result.
            if (manifest != default)
            {
                return Ok(manifest.ExtractFields(expandFields));
            }

            // Create an error model indicating the manifest was not found.
            var error404 = new GenericErrorModel(HttpContext)
                .AddError(nameof(key), $"Plugin manifest with key '{key}' not found on repository '{repository.Url}' for plugin type '{pluginType}'.")
                .SetRequest(request: repository);

            // Return a 404 Not Found response with the error model.
            return NotFound(error404);
        }

        [HttpGet]
        [Route("manifests")]
        [SwaggerOperation(
            summary: "Retrieves all plugin manifests.",
            description: "Fetches all available plugin manifests, optionally filtering the response based on the fields provided in the `expandFields` query parameter. Returns the manifest data in JSON format.",
            Tags = ["Integration", "Manifests"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully retrieved the list of plugin manifests.", type: typeof(IG4PluginManifest[]), contentTypes: MediaTypeNames.Application.Json)]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client, NoStore = false)]
        public IActionResult GetManifests(
            [SwaggerParameter(description: "Comma-separated list of fields to include in the response.", Required = false)] string expandFields)
        {
            // Retrieve all manifests and optionally extract only the specified fields using 'expandFields' if provided.
            var manifests = _domain
                .G4
                .Integration
                .GetManifests<IG4PluginManifest>()
                .Select(i => i.ExtractFields(expandFields))
                .ToArray();

            // Return a cusom response header indicating the total number of manifests.
            Response.Headers.Append("X-Manifest-Count", $"{manifests.Length}");

            // Return the filtered list of manifests in JSON format.
            return Ok(manifests);
        }

        [HttpPost]
        [Route("manifests")]
        [SwaggerOperation(
            summary: "Retrieves plugin manifests from external repositories.",
            description: "Fetches plugin manifests from the specified external repositories. Optionally includes specific fields in the response based on the `expandFields` query parameter. Returns the manifest data in JSON format.",
            Tags = ["Integration", "Manifests"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "Successfully retrieved the plugin manifests.", type: typeof(IG4PluginManifest[]), contentTypes: MediaTypeNames.Application.Json)]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client, NoStore = false)]
        public IActionResult GetManifests(
            [SwaggerParameter(description: "Comma-separated list of fields to include in the response.", Required = false)] string expandFields,
            [SwaggerParameter(description: "Array of external repository information from where the plugin manifests are retrieved.", Required = true)][FromBody] G4ExternalRepositoryModel[] repositories)
        {
            // Retrieve all manifests from the provided repositories and optionally filter fields using 'expandFields'.
            var manifests = _domain
                .G4
                .Integration
                .GetManifests<IG4PluginManifest>(repositories)
                .Select(i => i.ExtractFields(expandFields))
                .ToArray();

            // Add the number of manifests retrieved to the response headers.
            Response.Headers.Append("X-Manifest-Count", $"{manifests.Length}");

            // Return the list of manifests in JSON format.
            return Ok(manifests);
        }
    }
}
