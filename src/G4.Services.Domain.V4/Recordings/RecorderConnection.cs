using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Recordings
{
    // Owns one endpoint's transport, browser identifier, and event buffer for exactly one API session.
    internal sealed class RecorderConnection : IAsyncDisposable
    {
        #region *** Constants    ***

        // Bounds the recorder-side flush so a recorder without the method, or a stalled one, never blocks stop.
        private const int FlushTimeoutSeconds = 5;

        // Relative recorder hub route served by every G4 backend; appended to the endpoint base URL.
        private const string HubPath = "/hub/v4/g4/peek";

        // Longest time stop waits for broadcasts already in flight once the recorder reports a drained queue.
        private const int QuietCeilingMilliseconds = 1500;

        // Silence on the transport for this long means the recorder has no more events in flight.
        private const int QuietMilliseconds = 300;

        // Bounds hub startup and browser shutdown so one unresponsive recorder never stalls its API session.
        private const int TransportTimeoutSeconds = 15;

        #endregion

        #region *** Fields       ***

        // Owns the subscription and reconnect lifecycle until session cleanup.
        private readonly HubConnection _connection;

        // Holds detached models rather than transport-owned JSON values.
        private readonly List<JsonObject> _events = [];

        // Serializes receive callbacks with the stop snapshot so export never observes a growing buffer.
        private readonly object _syncRoot = new();

        // Tracks the browser launched by this connection; UIA never assigns a process identifier.
        private int? _browserProcessId;

        // Carries the latest transport or cleanup failure into REST status snapshots; null until a failure occurs.
        private string _error;

        // Stops new events at the API's received-before-stop boundary.
        private bool _isAcceptingEvents = true;

        // Monotonic timestamp of the most recent buffered event; zero until the first event arrives.
        private long _lastEventTimestamp;

        // Claimed at the start of disposal so repeated or concurrent cleanup calls run the release only once.
        private bool _isDisposed;

        // Drops incoming events while the client has paused this recorder; the transport stays connected.
        private bool _isSuspended;

        #endregion

        #region *** Constructors ***

        // Registers the receive handler before connecting so early recorder events are retained.
        internal RecorderConnection(RecorderModel options)
        {
            // Keep the detached configuration and build an auto-reconnecting hub on this endpoint's recorder route.
            Options = options;
            var hubUrl = options.BaseUrl + HubPath;
            _connection = new HubConnectionBuilder()
                .WithUrl(url: hubUrl)
                .WithAutomaticReconnect()
                .Build();

            // The backend broadcasts envelopes with an inner value model on both recorder families.
            _connection.On<JsonObject>(methodName: "ReceiveRecordingEvent", handler: message =>
            {
                // Buffer a detached copy only while capture is open, so events after stop never reach the export.
                lock (_syncRoot)
                {
                    var isBuffering = _isAcceptingEvents && !_isSuspended;
                    if (isBuffering && message["value"] is JsonObject model)
                    {
                        _events.Add(item: (JsonObject)model.DeepClone());
                        _lastEventTimestamp = Stopwatch.GetTimestamp();
                    }
                }
            });
        }

        #endregion

        #region *** Properties   ***

        // Chromium launches a browser; UIA only subscribes to desktop capture.
        internal bool IsChromium
        {
            get
            {
                // Match the driver name case-insensitively so every Chrome driver spelling selects the browser flow.
                return Options.DriverName.Contains(value: "chrome", comparisonType: StringComparison.OrdinalIgnoreCase);
            }
        }

        // Configuration is detached from the request before this connection is allocated.
        internal RecorderModel Options { get; }

        #endregion

        #region *** Methods      ***

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            // Claim disposal under the lock so a failed start racing session cleanup releases the recorder only once.
            lock (_syncRoot)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
            }

            // Close the browser before its hub so Chromium receives the graceful shutdown request.
            try
            {
                // Bound the shutdown request so an unresponsive recorder never blocks session cleanup.
                using var timeout = new CancellationTokenSource(delay: TimeSpan.FromSeconds(seconds: TransportTimeoutSeconds));
                if (_browserProcessId is int processId)
                {
                    // Report a negative acknowledgement without discarding the exported automation.
                    var isStopped = await _connection
                        .InvokeAsync<bool>(methodName: "StopRecorder", arg1: processId, cancellationToken: timeout.Token)
                        .ConfigureAwait(false);
                    if (!isStopped)
                    {
                        var message = "The recorder did not acknowledge browser shutdown.";
                        SetError(instance: this, message);
                    }
                }
            }
            catch (Exception exception)
            {
                // Retain cleanup diagnostics while continuing to dispose the capture connection.
                SetError(instance: this, message: exception.Message);
            }
            finally
            {
                // Stop reconnect attempts even when browser shutdown fails or the transport has been lost.
                try
                {
                    await _connection.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    // Record the hub disposal failure for status snapshots; disposal still completes for the caller.
                    SetError(instance: this, message: exception.Message);
                }

                // Close the buffer and forget the browser so status snapshots report a fully released recorder.
                lock (_syncRoot)
                {
                    _isAcceptingEvents = false;
                    _browserProcessId = null;
                }
            }
        }

        // Waits for events the recorder captured before stop but has not delivered yet. The UIA recorder resolves
        // input on a background worker and can lag the physical input by up to a second, so stopping the buffer
        // immediately lost the final keys. Recorders without FlushRecorder (Chromium) use the quiet wait alone.
        // Never throws: a failed flush only shortens the wait, and the buffered events are exported regardless.
        internal async Task FlushAsync()
        {
            // Ask the recorder to drain its capture queue, bounded so an unresponsive recorder cannot stall stop.
            try
            {
                var flushTimeout = TimeSpan.FromSeconds(seconds: FlushTimeoutSeconds);
                using var timeout = new CancellationTokenSource(delay: flushTimeout);
                if (_connection.State == HubConnectionState.Connected)
                {
                    await _connection
                        .InvokeAsync<bool>(methodName: "FlushRecorder", cancellationToken: timeout.Token)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is HubException or OperationCanceledException
                or InvalidOperationException)
            {
                // A missing method, a timeout, or a closing transport falls through to the quiet wait.
            }

            // Wait until the transport has been silent long enough for the recorder's last broadcasts to land.
            // Silence is measured from the acknowledgement at the earliest, because the client dispatches event
            // handlers on its own loop and can complete the flush before handling events sent ahead of it.
            var flushedTimestamp = Stopwatch.GetTimestamp();
            var ceiling = Stopwatch.StartNew();
            while (ceiling.ElapsedMilliseconds < QuietCeilingMilliseconds)
            {
                // Read the last arrival under the lock that the receive handler writes it under.
                long lastEventTimestamp;
                lock (_syncRoot)
                {
                    lastEventTimestamp = _lastEventTimestamp;
                }

                // Stop waiting once neither the acknowledgement nor any event is recent.
                var quietStart = Math.Max(val1: lastEventTimestamp, val2: flushedTimestamp);
                var silence = Stopwatch.GetElapsedTime(startingTimestamp: quietStart);
                if (silence.TotalMilliseconds >= QuietMilliseconds)
                {
                    return;
                }

                await Task.Delay(millisecondsDelay: 25).ConfigureAwait(false);
            }
        }

        // Returns a detached diagnostic snapshot without exposing the driver's credentials.
        internal RecorderStatusModel GetStatus()
        {
            // Read every field under one lock so the snapshot never mixes pre- and post-cleanup state.
            lock (_syncRoot)
            {
                return new RecorderStatusModel
                {
                    BaseUrl = Options.BaseUrl,
                    BrowserProcessId = _browserProcessId,
                    Connected = _connection.State == HubConnectionState.Connected,
                    Error = _error,
                    EventCount = _events.Count,
                    Suspended = _isSuspended
                };
            }
        }

        // Resumes buffering after a suspend; events that arrived while suspended stay dropped.
        internal void Resume()
        {
            // Publish under the receive lock so the next event observes the resumed state.
            lock (_syncRoot)
            {
                _isSuspended = false;
            }
        }

        // Connects before launch so browser events reach this consumer from the first interaction.
        // Failures are recorded on this connection and never thrown, so peer recorders continue starting.
        internal async Task StartAsync(CancellationToken cancellationToken)
        {
            // Bound the happy-path startup rather than introducing a background retry worker.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token: cancellationToken);
            timeout.CancelAfter(delay: TimeSpan.FromSeconds(seconds: TransportTimeoutSeconds));
            try
            {
                // Open the hub before any launch so the receive handler is live for the first browser event.
                await _connection.StartAsync(cancellationToken: timeout.Token).ConfigureAwait(false);
                if (IsChromium)
                {
                    // Keep arbitrary driver parameters intact when invoking the existing browser launcher.
                    var processId = await _connection
                        .InvokeAsync<int>(
                            methodName: "StartRecorder",
                            arg1: Options.DriverParameters,
                            cancellationToken: timeout.Token)
                        .ConfigureAwait(false);

                    // Publish the process identifier under the lock so disposal and status read a consistent value.
                    lock (_syncRoot)
                    {
                        _browserProcessId = processId;
                    }
                }
            }
            catch (Exception exception)
            {
                // Isolate failure to this recorder and release resources before peers finish startup.
                SetError(instance: this, message: exception.Message);
                await DisposeAsync().ConfigureAwait(false);
            }
        }

        // Prevents all subsequent callbacks from changing the captured result and returns owned event copies.
        internal JsonObject[] StopBuffer()
        {
            // Close capture and copy the buffer in one critical section so no event lands between the two steps.
            lock (_syncRoot)
            {
                _isAcceptingEvents = false;
                return _events.Select(selector: model => (JsonObject)model.DeepClone()).ToArray();
            }
        }

        // Pauses buffering without closing the transport, so resuming needs no reconnect or browser relaunch.
        internal void Suspend()
        {
            // Publish under the receive lock so no event is buffered after this call returns.
            lock (_syncRoot)
            {
                _isSuspended = true;
            }
        }

        // Shares one lock with diagnostics so receive and cleanup never publish torn recorder state.
        // Overwrites any earlier failure; status snapshots report only the most recent error.
        private static void SetError(RecorderConnection instance, string message)
        {
            lock (instance._syncRoot)
            {
                instance._error = message;
            }
        }

        #endregion
    }
}
