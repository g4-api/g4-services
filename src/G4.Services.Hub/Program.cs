using G4.Converters;
using G4.Services.Domain.V4;
using G4.Services.Domain.V4.Extensions;
using G4.Services.Domain.V4.Formatters;
using G4.Services.Domain.V4.Hubs;
using G4.Services.Domain.V4.Middlewares;
using G4.Settings;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

// Write the ASCII logo for the Hub Controller with the specified version.
ControllerUtilities.WriteHubAsciiLogo(version: "0000.00.00.0000");

// Create a new instance of the WebApplicationBuilder with the provided command-line arguments.
var builder = WebApplication.CreateBuilder(args);

#region *** Url & Kestrel ***
// Configure the URLs that the Kestrel web server should listen on.
// If no URLs are specified, it uses the default settings.
builder.WebHost.UseUrls();
#endregion

#region *** Service       ***
// Add compression services to reduce the size of HTTP responses.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// Add routing services with configuration to use lowercase URLs for consistency and SEO benefits.
builder.Services.AddRouting(i => i.LowercaseUrls = true);

// Add support for Razor Pages, enabling server-side rendering of web pages.
builder.Services.AddRazorPages();

// Enable directory browsing, allowing users to see the list of files in a directory.
builder.Services.AddDirectoryBrowser();

// Add response compression services to reduce the size of HTTP responses.
// This is enabled for HTTPS requests to improve performance.
builder.Services.AddResponseCompression(i => i.EnableForHttps = true);

// Add controller services with custom input formatters and JSON serialization options.
builder.Services
    .AddControllers(i =>
        // Add a custom input formatter to handle plain text inputs.
        i.InputFormatters.Add(new PlainTextInputFormatter()))
    .AddJsonOptions(i =>
    {
        // Configure JSON serializer to format JSON with indentation for readability.
        i.JsonSerializerOptions.WriteIndented = false;

        // Ignore properties with null values during serialization to reduce payload size.
        i.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

        // Use camelCase naming for JSON properties to follow JavaScript conventions.
        i.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        // Enable case-insensitive property name matching during deserialization.
        i.JsonSerializerOptions.PropertyNameCaseInsensitive = true;

        // Add a custom type converter for handling specific types during serialization/deserialization.
        i.JsonSerializerOptions.Converters.Add(new TypeConverter());

        // Add a custom exception converter to handle exception serialization.
        i.JsonSerializerOptions.Converters.Add(new ExceptionConverter());

        // Add a custom DateTime converter to handle ISO 8601 date/time format.
        i.JsonSerializerOptions.Converters.Add(new DateTimeIso8601Converter());

        // Add a custom method base converter to handle method base serialization.
        i.JsonSerializerOptions.Converters.Add(new MethodBaseConverter());
    });

// Add and configure Swagger for API documentation and testing.
builder.Services.AddSwaggerGen(i =>
{
    // Define a Swagger document named "v4" with title and version information.
    i.SwaggerDoc(
        name: $"v{AppSettings.ApiVersion}",
        info: new OpenApiInfo { Title = "G4™ Hub Controllers", Version = $"v{AppSettings.ApiVersion}" });

    // Order API actions in the Swagger UI by HTTP method for better organization.
    i.OrderActionsBy(a => a.HttpMethod);

    // Enable annotations to allow for additional metadata in Swagger documentation.
    i.EnableAnnotations();
});

// Configure cookie policy options to manage user consent and cookie behavior.
builder.Services.Configure<CookiePolicyOptions>(i =>
{
    // Determine whether user consent is required for non-essential cookies.
    i.CheckConsentNeeded = _ => true;

    // Set the minimum SameSite policy to None, allowing cookies to be sent with cross-site requests.
    i.MinimumSameSitePolicy = SameSiteMode.None;
});

// Get origins from environment variable (with semicolon separation)
var originsEnvironmentParameter = Environment.GetEnvironmentVariable("ORIGINS");

// Normalize origins from environment variable or configuration
var origins = string.IsNullOrEmpty(originsEnvironmentParameter)
    ? builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? []
    : originsEnvironmentParameter.Split(";", StringSplitOptions.TrimEntries);

// Add and configure CORS (Cross-Origin Resource Sharing) to allow requests from any origin.
builder.Services.AddCors(options =>
    options.AddPolicy("CorsPolicy", policy => policy
        .SetIsOriginAllowed(origin =>
            origins.Contains(origin)
            || (origin != null && origin.StartsWith("vscode-webview://"))
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
    )
);

// Add and configure SignalR for real-time web functionalities.
builder.Services
    .AddSignalR((i) =>
    {
        // Enable detailed error messages for debugging purposes.
        i.EnableDetailedErrors = true;

        // Set the maximum size of incoming messages to the largest possible value.
        i.MaximumReceiveMessageSize = long.MaxValue;

        // How often the server sends a keep-alive ping. Default is 15 seconds.
        i.KeepAliveInterval = TimeSpan.FromSeconds(15);

        // If the server hasn't heard from a client in this much time, it might consider the client disconnected.
        // Usually the clientTimeout is set higher than KeepAliveInterval.
        i.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    })
    .AddJsonProtocol((i) =>
    {
        i.PayloadSerializerOptions = new JsonSerializerOptions
        {
            // Configure JSON serializer to format JSON with indentation for readability.
            WriteIndented = false,

            // Ignore properties with null values during serialization to reduce payload size.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

            // Use camelCase naming for JSON properties to follow JavaScript conventions.
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

            // Enable case-insensitive property name matching during deserialization.
            PropertyNameCaseInsensitive = true,

            // Add a custom type converter for handling specific types during serialization/deserialization.
            Converters =
            {
                new TypeConverter(),
                new ExceptionConverter(),
                new DateTimeIso8601Converter(),
                new MethodBaseConverter()
            }
        };
    });

// Add IHttpClientFactory to the service collection for making HTTP requests.
builder.Services.AddHttpClient();
#endregion

#region *** Dependencies  ***
// Configure dependencies for G4Domain.
G4Domain.SetDependencies(builder);
#endregion

#region *** Configuration ***
// Initialize the application builder
var app = builder.Build();

// Configure the application to use compression for responses
app.UseResponseCompression();

// Configure the application to use the exception handling middleware
app.UseMiddleware<ErrorHandlingMiddleware>();

// Configure the application to use the response caching middleware
app.UseResponseCaching();

// Add the cookie policy
app.UseCookiePolicy();

// Add the routing and controller mapping to the application
app.UseRouting();

// Add the CORS policy to the application to allow cross-origin requests
app.UseCors("CorsPolicy");

// Add the Swagger documentation and UI page to the application
app.UseSwagger();
app.UseSwaggerUI(i =>
{
    i.SwaggerEndpoint($"/swagger/v{AppSettings.ApiVersion}/swagger.json", $"G{AppSettings.ApiVersion}");
    i.DisplayRequestDuration();
    i.EnableFilter();
    i.EnableTryItOutByDefault();
});

app.MapDefaultControllerRoute();
app.MapControllers();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        // Get the file extension in lower-case.
        var extension = Path.GetExtension(context.File.Name)?.ToLowerInvariant();
        if (extension == null)
        {
            return;
        }

        // Set the Content-Type header with charset based on file extension.
        switch (extension)
        {
            case ".html":
                context.Context.Response.Headers.ContentType = "text/html; charset=utf-8";
                break;
            case ".css":
                context.Context.Response.Headers.ContentType = "text/css; charset=utf-8";
                break;
            case ".js":
                context.Context.Response.Headers.ContentType = "application/javascript; charset=utf-8";
                break;
            case ".json":
                context.Context.Response.Headers.ContentType = "application/json; charset=utf-8";
                break;
            case ".svg":
                context.Context.Response.Headers.ContentType = "image/svg+xml; charset=utf-8";
                break;
            default:
                // Optionally, do nothing for other file types.
                break;
        }
    }
});

// Add the SignalR hub to the application for real-time communication with clients and other services
app.MapHub<G4Hub>($"/hub/v{AppSettings.ApiVersion}/g4/orchestrator").RequireCors("CorsPolicy");

// Add the SignalR hub to send automation notifications to clients and other services in real-time
app.MapHub<G4AutomationNotificationsHub>($"/hub/v{AppSettings.ApiVersion}/g4/notifications").RequireCors("CorsPolicy");

// Add the signalR hub the bots endpoint to send and receive messages in real-time
app.MapHub<G4BotsHub>($"/hub/v{AppSettings.ApiVersion}/g4/bots").RequireCors("CorsPolicy");
#endregion

// Retrieve the logger service and log that the application has started.
using (var scope = app.Services.CreateScope())
{
    scope
        .ServiceProvider
        .GetRequiredService<ILogger>()?
        .LogInformation("Service application initialized successfully.");
}

// Start the application and wait for it to finish.
await app.RunAsync();
