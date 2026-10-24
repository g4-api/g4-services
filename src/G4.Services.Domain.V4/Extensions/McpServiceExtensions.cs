using G4.Services.Domain.V4.Mcp;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;

namespace G4.Services.Domain.V4.Extensions
{
    /// <summary>
    /// Registers and maps the SDK-hosted G4 MCP server.
    /// </summary>
    /// <remarks>
    /// The server exposes only the attribute-declared tool types listed by <see cref="McpToolTypes"/>. G4 catalog
    /// (plugin) tools have no MCP entry of their own: clients reach them only through the router tools, discovering
    /// them with g4.FindTools or g4.FindTool and running them with g4.SendRule or g4.SendRules, so a tools/call for
    /// any other name receives the SDK's unknown-tool error. The transport is stateless Streamable HTTP, so every
    /// POST is self-contained, no MCP session identifier is issued, and server-initiated notifications such as
    /// tools/list_changed are unavailable; clients re-list tools to observe catalog changes.
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
        public const string ServerName = "g4-engine-mcp";

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
        /// caller.
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

        #endregion
    }
}
