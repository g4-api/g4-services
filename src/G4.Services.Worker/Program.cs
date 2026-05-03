using CommandBridge;

using G4.Extensions;
using G4.Services.Domain.V4;
using G4.Services.Domain.V4.Extensions;
using G4.Services.Domain.V4.Formatters;
using G4.Services.Domain.V4.Middlewares;
using G4.Settings;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

// Write the ASCII logo for the Worker Controller with the specified version.
ControllerUtilities.WriteWorkerAsciiLogo(version: "0000.00.00.0000");

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
    .AddControllers(i => i.InputFormatters.Add(new PlainTextInputFormatter()))
    .AddJsonOptions(i => i.New(AppSettings.JsonOptions));

// Add and configure Swagger for API documentation and testing.
builder.Services.AddSwaggerGen(i =>
{
    // Define a Swagger document named "v4" with title and version information.
    i.SwaggerDoc(
        name: $"v{AppSettings.ApiVersion}",
        info: new OpenApiInfo { Title = "G4™ Worker Controllers", Version = $"v{AppSettings.ApiVersion}" });

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

// Add and configure CORS (Cross-Origin Resource Sharing) to allow requests from any origin.
builder.Services
    .AddCors(i =>
        i.AddPolicy("CorsPolicy", builder =>
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader()));

// Add and configure SignalR for real-time web functionalities.
builder.Services
    .AddSignalR(i => i.New(AppSettings.HubOptions))
    .AddJsonProtocol(i => i.PayloadSerializerOptions = AppSettings.JsonOptions);
#endregion

#region *** Dependencies  ***
// Configure dependencies for G4Domain.
IDomain.SetDependencies(builder);
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

// Add the routing and controller mapping to the application
app.UseRouting();
app.MapDefaultControllerRoute();
app.MapControllers();
#endregion

// Retrieve the logger service and log that the application has started.
using (var scope = app.Services.CreateScope())
{
    scope
        .ServiceProvider
        .GetRequiredService<ILogger>()?
        .LogInformation("Service application initialized successfully.");
}

// Attempt to locate and invoke the command based on the command-line arguments.
var command = CommandBase.FindCommand(args);
command?.Invoke(args);

// Create and start a new G4HubListener to handle pending automation tasks in the background.
new G4HubListener().StartG4HubListener();

// Start the application and wait for it to finish.
await app.RunAsync();
