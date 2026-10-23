using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.Linq;
using System.Net;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace G4.Services.UnitTests.Recordings
{
    // Hosts the existing recorder hub contract on a controlled real socket without launching desktop applications.
    internal sealed class TestRecorderServer(WebApplication application) : IAsyncDisposable
    {
        #region *** Fields       ***

        // Retains the app so each test releases its listener and connected clients deterministically.
        private readonly WebApplication _application = application;

        #endregion

        #region *** Properties   ***

        // The operating system assigns a free port, keeping concurrent test hosts isolated.
        internal string BaseUrl
        {
            get
            {
                // Read the bound listener after startup so callers use the port assigned by the operating system.
                var server = _application.Services.GetRequiredService<IServer>();
                var addresses = server.Features.Get<IServerAddressesFeature>().Addresses;
                return addresses.Single();
            }
        }

        // The hub records calls so REST tests can prove browser ownership and repeated-stop behavior.
        internal TestRecorderState State => _application.Services.GetRequiredService<TestRecorderState>();

        #endregion

        #region *** Methods      ***

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            // Cleanup: close real sockets even when a REST assertion fails.
            await _application.DisposeAsync().ConfigureAwait(false);
        }

        // Creates a deterministic backend fixture using the same SignalR route as the G4 recorders.
        internal static async Task<TestRecorderServer> NewAsync()
        {
            // Arrange: use loopback and an OS-assigned port rather than a shared hard-coded endpoint.
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.Sources.Clear();
            builder.WebHost.ConfigureKestrel(options: options => options.Listen(address: IPAddress.Loopback, port: 0));
            builder.Logging.ClearProviders();
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<TestRecorderState>();

            // Arrange: map the production hub route only after this fixture owns its isolated host and state.
            var application = builder.Build();
            application.MapHub<TestRecorderHub>(pattern: "/hub/v4/g4/peek");

            // Arrange: start before returning so API connection attempts exercise an available recorder.
            await application.StartAsync().ConfigureAwait(false);
            return new TestRecorderServer(application);
        }

        // Sends the exact G4 envelope contract over the real SignalR transport.
        internal async Task SendAsync(JsonObject model)
        {
            // Act: publish to every recorder consumer, reproducing backend broadcast semantics.
            var hub = _application.Services.GetRequiredService<IHubContext<TestRecorderHub>>();
            await hub.Clients.All.SendAsync(method: "ReceiveRecordingEvent", arg1: new { value = model }).ConfigureAwait(false);
        }

        #endregion
    }

    // Records browser lifecycle invocations independently for each test recorder endpoint.
    internal sealed class TestRecorderState
    {
        #region *** Properties   ***

        // Preserves the launch payload so arbitrary driver capabilities can be asserted after HTTP deserialization.
        internal JsonObject DriverParameters { get; set; }

        // Simulates a recorder-owned browser-launch failure without waiting on an unavailable network endpoint.
        internal bool FailStart { get; set; }

        // Counts flush requests so stop can be shown to drain the recorder before freezing its buffer.
        internal int FlushCount { get; set; }

        // Models an event the recorder captured before stop but delivers only while draining its queue.
        internal JsonObject PendingEvent { get; set; }

        // Counts launch requests to detect duplicate lifecycle operations.
        internal int StartCount { get; set; }

        // Counts shutdown requests to verify repeated REST stop is idempotent.
        internal int StopCount { get; set; }

        // Retains the requested process identifier so stop can be tied to this host's launch result.
        internal int? StoppedProcessId { get; set; }

        #endregion
    }

    // Implements only the recorder methods consumed by the prototype, with no OS process side effects.
    internal sealed class TestRecorderHub(TestRecorderState state) : Hub
    {
        #region *** Methods      ***

        /// <summary>
        /// Delivers the pending event, as a recorder draining its capture queue does, then acknowledges the flush.
        /// </summary>
        /// <returns>True for a drained queue.</returns>
        public async Task<bool> FlushRecorder()
        {
            // Arrange: count the request so tests can prove stop asked the recorder to drain.
            state.FlushCount++;

            // Arrange: broadcast the trailing event before the acknowledgement, matching the recorder's ordering.
            if (state.PendingEvent is JsonObject pending)
            {
                state.PendingEvent = null;
                var envelope = new { value = pending };
                await Clients.All.SendAsync(method: "ReceiveRecordingEvent", arg1: envelope).ConfigureAwait(false);
            }

            return true;
        }

        /// <summary>
        /// Captures the exact driver payload and returns a deterministic browser identifier to the API client.
        /// </summary>
        /// <param name="driverParameters">The JSON launch configuration sent by the client.</param>
        /// <returns>The fixture browser identifier.</returns>
        public int StartRecorder(JsonObject driverParameters)
        {
            // Arrange: retain launch ownership and payload for lifecycle assertions outside the hub call.
            state.DriverParameters = driverParameters;
            state.StartCount++;

            // Arrange: fail after observing the launch attempt so tests can distinguish retry from duplicate startup.
            if (state.FailStart)
            {
                throw new HubException(message: "Fixture browser launch failed.");
            }

            return 12345;
        }

        /// <summary>
        /// Records browser shutdown and acknowledges it without manipulating a real operating-system process.
        /// </summary>
        /// <param name="processId">The identifier returned by the fixture launch.</param>
        /// <returns>True for an accepted stop request.</returns>
        public bool StopRecorder(int processId)
        {
            // Arrange: retain the stop count to detect duplicate work from repeated REST requests.
            state.StoppedProcessId = processId;
            state.StopCount++;
            return true;
        }

        #endregion
    }
}
