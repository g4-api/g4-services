using G4.Models;
using G4.Services.Domain.V4.Mcp;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using System;

namespace G4.Services.Domain.V4.Extensions
{
    /// <summary>
    /// Registers and maps the SDK-hosted G4 MCP server.
    /// </summary>
    /// <remarks>
    /// The server exposes only the attribute-declared tool types listed by <see cref="McpToolTypes"/>. G4 catalog
    /// (plugin) tools have no MCP entry of their own: clients reach them only through the router tools, discovering
    /// them with g4.ResolveTools, g4.FindTools, or g4.FindTool and running them with g4.SendRule or g4.SendRules,
    /// so a tools/call for any other name receives the SDK's unknown-tool error. The transport is stateless Streamable
    /// HTTP, so every POST is self-contained, no MCP session identifier is issued, and server-initiated notifications
    /// such as tools/list_changed are unavailable; clients re-list tools to observe catalog changes.
    /// </remarks>
    public static class McpServiceExtensions
    {
        #region *** Constants    ***

        /// <summary>
        /// Specifies the route of the MCP Streamable HTTP endpoint.
        /// </summary>
        /// <remarks>
        /// The Hub pipeline, the Swagger document filter, and the tests share this value, so the endpoint, its
        /// error-handling exclusion, and its documentation never drift apart.
        /// </remarks>
        public const string McpRoute = "/api/v4/g4/mcp";

        /// <summary>
        /// Specifies the server name reported to MCP clients during initialization.
        /// </summary>
        /// <remarks>
        /// The name is an identifier clients may match on, so branding never changes it; the branded display name is
        /// published as the server title instead.
        /// </remarks>
        public const string ServerName = "g4-engine-mcp";

        // The server title reported to MCP clients, written with branding placeholders that the Hub resolves.
        private const string ServerTitle = "{engine} MCP";

        /// <summary>
        /// Specifies the server version reported to MCP clients during initialization.
        /// </summary>
        public const string ServerVersion = "4.0.0";

        #endregion

        #region *** Methods      ***

        /// <summary>
        /// Registers the MCP server, its stateless HTTP transport, and the attribute-declared tools.
        /// </summary>
        /// <remarks>
        /// The system tools resolve <see cref="Repositories.IToolsRepository"/> and the recording tools resolve
        /// <see cref="Repositories.IRecordingsRepository"/> from the container, so both must be registered by the
        /// caller. Tool titles, descriptions, and argument descriptions are written with branding placeholders; they
        /// are resolved once, after the tools are collected, from the registered <see cref="BrandingSettingsModel"/>,
        /// or from the default branding when none is registered.
        /// </remarks>
        /// <param name="services">The service collection that receives the MCP server registrations.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public static IServiceCollection AddG4Mcp(this IServiceCollection services)
        {
            // Configure server identity and advertise tools without the list-change notifications that a stateless
            // server cannot send.
            services
                .AddMcpServer(configureOptions: options =>
                {
                    options.ServerInfo = new Implementation { Name = ServerName, Version = ServerVersion };
                    options.Capabilities = new ServerCapabilities
                    {
                        Tools = new ToolsCapability { ListChanged = false }
                    };
                })
                .WithHttpTransport(configureOptions: options => options.SessionMode = HttpServerSessionMode.Stateless)
                .WithTools(toolTypes: McpToolTypes.Types);

            // Brand the collected tools and the server title once, after the SDK has added every tool, so tools/list
            // publishes branded text without work per request.
            services
                .AddOptions<McpServerOptions>()
                .PostConfigure<IServiceProvider>((options, provider) =>
                {
                    var branding = provider.GetService<BrandingSettingsModel>() ?? BrandingExtensions.NewDefaultBranding();
                    SetBranding(options, branding);
                });

            return services;
        }

        /// <summary>
        /// Maps the MCP Streamable HTTP endpoint at <see cref="McpRoute"/>.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder of the hosting application.</param>
        /// <returns>The convention builder of the mapped endpoint, for further configuration such as CORS.</returns>
        public static IEndpointConventionBuilder MapG4Mcp(this IEndpointRouteBuilder endpoints)
        {
            // Delegate to the SDK so protocol negotiation, framing, and JSON-RPC errors follow the specification.
            return endpoints.MapMcp(pattern: McpRoute);
        }

        // Applies the branding to the server title and to every collected tool. The protocol tools are shared for the
        // server lifetime, so this runs once at startup.
        private static void SetBranding(McpServerOptions options, BrandingSettingsModel branding)
        {
            // Publish the branded server title next to the unchanged server name.
            if (options.ServerInfo is not null)
            {
                options.ServerInfo.Title = branding.ResolveText(ServerTitle);
            }

            // Brand each collected tool with the same rules the G4 tool catalog applies to its definitions.
            foreach (var tool in options.ToolCollection ?? [])
            {
                McpToolTypes.SetBranding(tool.ProtocolTool, branding);
            }
        }

        #endregion
    }
}
