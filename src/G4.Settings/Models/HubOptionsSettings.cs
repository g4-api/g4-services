using System;
using System.Collections.Generic;

namespace G4.Settings.Models
{
    /// <summary>
    /// Represents configurable SignalR hub options used to control connection behavior,
    /// protocol support, message limits, and streaming settings.
    /// </summary>
    /// <remarks>
    /// This model mirrors the main hub runtime settings that affect how clients connect,
    /// stay connected, exchange messages, and interact with dependency injection.
    /// </remarks>
    public class HubOptionsSettings
    {
        /// <summary>
        /// Gets or sets the maximum amount of time the server waits to receive
        /// a message from a connected client before closing the connection.
        /// </summary>
        /// <remarks>When not specified, the default timeout is 30 seconds.</remarks>
        public TimeSpan? ClientTimeoutInterval { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Gets or sets a value indicating whether hub method parameters should be
        /// prevented from being resolved implicitly from the dependency injection container.
        /// </summary>
        /// <remarks>
        /// When set to <see langword="false"/>, IServiceProviderIsService
        /// is used to determine whether a hub method parameter can be injected from
        /// the DI container. Parameters can still be explicitly marked with metadata
        /// that implements <see cref="IFromServiceMetadata"/>.
        /// </remarks>
        public bool DisableImplicitFromServicesParameters { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether detailed server-side exception
        /// information is included in errors sent back to clients.
        /// </summary>
        /// <remarks>
        /// Enabling this option can make troubleshooting easier, but it may expose
        /// internal implementation details to connected clients.
        /// </remarks>
        public bool? EnableDetailedErrors { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum time the server waits for a client to complete
        /// the SignalR handshake after connecting.
        /// </summary>
        /// <remarks>When not specified, the default timeout is 15 seconds.</remarks>
        public TimeSpan? HandshakeTimeout { get; set; }

        /// <summary>
        /// Gets or sets the interval used by the server to send keep-alive pings
        /// to connected clients.
        /// </summary>
        /// <remarks>When not specified, the default interval is 15 seconds.</remarks>
        public TimeSpan? KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Gets or sets the maximum size, in bytes, of a single incoming hub message.
        /// </summary>
        /// <remarks>When not specified, the default limit is 32 KB.</remarks>
        public long? MaximumReceiveMessageSize { get; set; } = long.MaxValue;

        /// <summary>
        /// Gets or sets the maximum number of bytes buffered per connection when
        /// stateful reconnect is enabled.
        /// </summary>
        /// <remarks>The default value is 100,000 bytes.</remarks>
        public long StatefulReconnectBufferSize { get; set; } = 100_000;

        /// <summary>
        /// Gets or sets the maximum number of items buffered for client upload streams.
        /// </summary>
        /// <remarks>When not specified, the default capacity is 10.</remarks>
        public int? StreamBufferCapacity { get; set; }

        /// <summary>
        /// Gets or sets the collection of hub protocol names that are allowed
        /// for client connections.
        /// </summary>
        public IList<string> SupportedProtocols { get; set; }
    }
}
