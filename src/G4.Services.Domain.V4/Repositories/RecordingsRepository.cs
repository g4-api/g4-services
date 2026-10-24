using G4.Services.Domain.V4.Recordings;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Owns in-memory recording sessions and endpoint reservations shared by the REST and MCP transports.
    /// </summary>
    /// <remarks>
    /// Registered as an application singleton, so a session started through one transport is visible to the other.
    /// Completed flows remain retrievable until process exit. No durable store or restart recovery is provided.
    /// Host shutdown disposes the repository, which stops every remaining session and closes owned browsers.
    /// </remarks>
    public sealed class RecordingsRepository : IRecordingsRepository, IAsyncDisposable
    {
        #region *** Constants    ***

        // Copy settings with the camel-case contract shared by the REST and MCP transports.
        private static readonly JsonSerializerOptions SerializerOptions = new(defaults: JsonSerializerDefaults.Web);

        #endregion

        #region *** Fields       ***

        // The backend broadcasts by endpoint, so this process permits one owning session per normalized endpoint.
        private readonly HashSet<string> _reservedEndpoints = new(comparer: StringComparer.OrdinalIgnoreCase);

        // Retains completed sessions so repeated stop and flow requests use the same immutable result.
        private readonly Dictionary<Guid, RecordingSession> _sessions = [];

        // Protects session allocation and endpoint reservations without holding a lock during network I/O.
        private readonly object _syncRoot = new();

        #endregion

        #region *** Methods      ***

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            // Snapshot sessions under the lock so stop calls run without holding it during network I/O.
            var sessions = Array.Empty<RecordingSession>();
            lock (_syncRoot)
            {
                sessions = _sessions.Values.ToArray();
            }

            // Let normal stop freeze buffers and release browser ownership during host shutdown.
            var failures = new List<Exception>();
            foreach (var session in sessions)
            {
                try
                {
                    await StopAsync(id: session.Id).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    // Collect the failure and continue so every remaining browser still receives its shutdown request.
                    failures.Add(item: exception);
                }
            }

            // Report every collected failure only after all sessions were attempted, so host shutdown logs each one.
            if (failures.Count > 0)
            {
                var message = "One or more recording sessions failed to stop during shutdown.";
                throw new AggregateException(message, innerExceptions: failures);
            }
        }

        /// <inheritdoc />
        public JsonObject GetAutomation(Guid id)
        {
            // Protect result publication while allowing independent REST callers to retain their own JSON copy.
            var session = GetSession(instance: this, id);
            lock (session.SyncRoot)
            {
                // Reject retrieval before stop so callers never receive a partial flow from a live recording.
                if (session.State != RecordingStates.Stopped)
                {
                    var message = "Stop the recording before requesting its automation.";
                    throw new InvalidOperationException(message);
                }

                // Clone the retained flow so callers own their copy; an empty recording stays null.
                return session.Automation?.DeepClone().AsObject();
            }
        }

        /// <inheritdoc />
        public RecordingStatusModel GetStatus(Guid id)
        {
            // Snapshot lifecycle data under its own lock; connections separately protect their event buffers.
            var session = GetSession(instance: this, id);
            lock (session.SyncRoot)
            {
                return new RecordingStatusModel
                {
                    Id = session.Id,
                    State = session.State,
                    HasAutomation = session.Automation is not null,
                    Jobs = session.Jobs.Select(selector: NewJobCopy).ToArray(),
                    Recorders = session.Connections.Select(selector: connection => connection.GetStatus()).ToArray()
                };
            }

            // Copies one job entry so status readers never share the session's retained map.
            static RecordingJobModel NewJobCopy(RecordingJobModel job) => new()
            {
                BaseUrl = job.BaseUrl,
                Id = job.Id,
                Index = job.Index
            };
        }

        /// <inheritdoc />
        public RecordingStatusModel ResumeRecorder(Guid id, string baseUrl)
        {
            return SetRecorderSuspended(instance: this, id, baseUrl, isSuspended: false);
        }

        /// <inheritdoc />
        public async Task<RecordingStatusModel> StartAsync(
            RecordingSettingsModel settings,
            CancellationToken cancellationToken)
        {
            // Validate and copy the settings before acquiring endpoint ownership or allocating transports.
            var session = NewSession(settings);

            try
            {
                lock (_syncRoot)
                {
                    // Reject the whole request when any endpoint is owned, so two sessions never share one broadcast.
                    var isEndpointReserved = session.Connections
                        .Any(predicate: connection => _reservedEndpoints.Contains(item: connection.Options.BaseUrl));
                    if (isEndpointReserved)
                    {
                        var message = "A requested recorder endpoint is already recording.";
                        throw new InvalidOperationException(message);
                    }

                    // Claim every endpoint and retain the session in one critical section so ownership is atomic.
                    foreach (var connection in session.Connections)
                    {
                        _reservedEndpoints.Add(item: connection.Options.BaseUrl);
                    }

                    _sessions.Add(key: session.Id, value: session);
                }
            }
            catch
            {
                // Rejected create attempts own unstarted transports, which still require disposal.
                var disposals = session.Connections.Select(selector: connection => connection.DisposeAsync().AsTask());
                await Task.WhenAll(tasks: disposals).ConfigureAwait(false);
                session.Gate.Dispose();
                throw;
            }

            // The session gate serializes shutdown with initial startup without serializing unrelated sessions.
            await session.Gate.WaitAsync(cancellationToken: CancellationToken.None).ConfigureAwait(false);
            try
            {
                // Start every endpoint concurrently; each connection records its own failure instead of throwing.
                var startups = session.Connections.Select(selector: connection => connection.StartAsync(cancellationToken));
                await Task.WhenAll(tasks: startups).ConfigureAwait(false);

                // A session is usable only while the caller still waits and at least one recorder connected.
                var isAnyConnected = session.Connections.Any(predicate: connection => connection.GetStatus().Connected);
                var isStartFailed = cancellationToken.IsCancellationRequested || !isAnyConnected;
                if (isStartFailed)
                {
                    // A failed create has no usable session; release its resources and reservations immediately.
                    await CloseConnectionsAsync(instance: this, session).ConfigureAwait(false);
                    lock (_syncRoot)
                    {
                        _sessions.Remove(key: session.Id);
                    }

                    // Surface caller cancellation unchanged; otherwise report every endpoint failure in one error.
                    cancellationToken.ThrowIfCancellationRequested();
                    var failureDetails = session.Connections
                        .Select(selector: connection => $"{connection.Options.BaseUrl}: {connection.GetStatus().Error}");
                    var failures = string.Join(separator: "; ", values: failureDetails);
                    var message = $"No recorder started. {failures}";
                    throw new HttpRequestException(message);
                }

                // Publish the Recording state under the session lock so status readers observe a completed start.
                lock (session.SyncRoot)
                {
                    session.State = RecordingStates.Recording;
                }

                return GetStatus(id: session.Id);
            }
            finally
            {
                // Release lifecycle ownership even when the HTTP caller disconnects or every endpoint fails.
                session.Gate.Release();
            }
        }

        /// <inheritdoc />
        public async Task<JsonObject> StopAsync(Guid id)
        {
            // Serialize repeated stop calls so browser shutdown and conversion execute only once.
            var session = GetSession(instance: this, id);
            await session.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                // Return the retained flow for repeated stops; otherwise mark Stopping so status shows the shutdown.
                lock (session.SyncRoot)
                {
                    if (session.State == RecordingStates.Stopped)
                    {
                        return session.Automation?.DeepClone().AsObject();
                    }

                    session.State = RecordingStates.Stopping;
                }

                try
                {
                    // Let every recorder deliver input it captured before stop, so trailing keys are not lost.
                    var flushes = session.Connections.Select(selector: connection => connection.FlushAsync());
                    await Task.WhenAll(tasks: flushes).ConfigureAwait(false);

                    // Freeze all buffers before conversion; shutdown events cannot enter the exported result.
                    var captured = session.Connections
                        .Select(selector: connection => new RecorderCapture(
                            Options: connection.Options,
                            Events: connection.StopBuffer()))
                        .ToArray();
                    var recording = AutomationBuilder.NewRecording(
                        authentication: session.Authentication,
                        captures: captured,
                        excludedProcessIds: session.ExcludedProcessIds,
                        excludedWindowTitles: session.ExcludedWindowTitles);

                    // Publish the flow and its job map together so readers never observe a half-assigned result.
                    lock (session.SyncRoot)
                    {
                        session.Automation = recording.Automation;
                        session.Jobs = recording.Jobs;
                    }
                }
                finally
                {
                    // Cleanup runs even when conversion fails and never replaces an already generated flow.
                    await CloseConnectionsAsync(instance: this, session).ConfigureAwait(false);
                    lock (session.SyncRoot)
                    {
                        session.State = RecordingStates.Stopped;
                    }
                }

                // Return a caller-owned copy through the same path used by flow retrieval.
                return GetAutomation(id);
            }
            finally
            {
                // Release the gate so later stop and flow requests can read the retained result.
                session.Gate.Release();
            }
        }

        /// <inheritdoc />
        public RecordingStatusModel SuspendRecorder(Guid id, string baseUrl)
        {
            return SetRecorderSuspended(instance: this, id, baseUrl, isSuspended: true);
        }

        // Releases every connection before freeing reservations so another session cannot race the old browser stop.
        // Connection disposal records its own failures, so reservations are always freed once disposal completes.
        private static async Task CloseConnectionsAsync(RecordingsRepository instance, RecordingSession session)
        {
            // Dispose all connections concurrently; each one stops its browser before closing its hub.
            var disposals = session.Connections.Select(selector: connection => connection.DisposeAsync().AsTask());
            await Task.WhenAll(tasks: disposals).ConfigureAwait(false);

            // Free the endpoints only after disposal so a new session never connects while the old browser closes.
            lock (instance._syncRoot)
            {
                foreach (var connection in session.Connections)
                {
                    instance._reservedEndpoints.Remove(item: connection.Options.BaseUrl);
                }
            }
        }

        // Round-trips through JSON so the session owns a detached copy, including nested driver parameters.
        // The caller-owned value is only read; later caller mutation never reaches the returned copy.
        private static T Copy<T>(T value)
        {
            // Serialize with the shared transport contract so the copy keeps every REST and MCP field.
            var json = JsonSerializer.Serialize(value, options: SerializerOptions);

            // Deserialize into a new graph so no reference is shared with the caller.
            return JsonSerializer.Deserialize<T>(json, options: SerializerOptions);
        }

        // Finds retained session ownership and keeps unknown identifiers distinct from invalid lifecycle requests.
        // Throws KeyNotFoundException, which the API maps to 404, for identifiers never issued or already discarded.
        private static RecordingSession GetSession(RecordingsRepository instance, Guid id)
        {
            // Read the session table under the service lock so lookups never race allocation or failed-start removal.
            lock (instance._syncRoot)
            {
                if (!instance._sessions.TryGetValue(key: id, value: out var session))
                {
                    throw new KeyNotFoundException(message: "The recording session was not found.");
                }

                return session;
            }
        }

        // Resolves enabled options from detached settings and rejects features that are not supported yet.
        // Returns a session whose connections are allocated but not started; the caller owns startup and disposal.
        private static RecordingSession NewSession(RecordingSettingsModel settings)
        {
            // Fail configuration errors before opening any network connection.
            AssertSettings(settings);

            // Copy authentication and recorders so later caller mutation never alters the session or exported flow.
            var authentication = Copy(value: settings.Authentication);
            var recorders = settings.Recorders ?? [];

            // Normalize every enabled endpoint and reject duplicate entries rather than silently overwriting them.
            var enabled = recorders
                .Where(predicate: recorder => recorder is not null && recorder.Enabled)
                .Select(selector: recorder => Copy(value: recorder))
                .ToArray();
            if (enabled.Length == 0)
            {
                var message = "At least one enabled recorder is required.";
                throw new ArgumentException(message, paramName: nameof(settings));
            }

            // Resolve each recorder in place so URLs are canonical before the uniqueness check compares them.
            foreach (var recorder in enabled)
            {
                ResolveRecorder(recorder);
            }

            // Compare canonical URLs case-insensitively so one endpoint never appears twice under different spellings.
            var uniqueEndpointCount = enabled
                .Select(selector: recorder => recorder.BaseUrl)
                .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
                .Count();
            if (uniqueEndpointCount != enabled.Length)
            {
                var message = "Enabled recorder endpoints must be unique.";
                throw new ArgumentException(message, paramName: nameof(settings));
            }

            // Allocate one unstarted connection per endpoint so startup can run them concurrently.
            var connections = enabled.Select(selector: recorder => new RecorderConnection(options: recorder)).ToArray();

            // Copy the window exclusions so later caller mutation never changes which events the flow drops.
            return new RecordingSession(authentication, connections)
            {
                ExcludedProcessIds = [.. settings.ExcludedProcessIds ?? []],
                ExcludedWindowTitles = [.. settings.ExcludedWindowTitles ?? []]
            };

            // Validates the envelope before copying or transport allocation; failures retain the settings parameter name.
            static void AssertSettings(RecordingSettingsModel settings)
            {
                // Require the envelope before reading its mandatory authentication.
                ArgumentNullException.ThrowIfNull(argument: settings);

                // Report absent authentication as caller input; MCP callers bypass MVC's required-field validation.
                if (settings.Authentication is null)
                {
                    var message = "Authentication is required.";
                    throw new ArgumentException(message, paramName: nameof(settings));
                }
            }
        }

        // Resolves URL and playback contracts on the session's detached recorder copy.
        // Mutates only the detached recorder model and throws ArgumentException for any unsupported configuration.
        private static void ResolveRecorder(RecorderModel recorder)
        {
            // Accept the existing schema/host/port shape and the REST convenience form through one canonical URL.
            var endpoint = recorder.BaseUrl ?? $"{recorder.Schema}://{recorder.Host}:{recorder.Port}";
            var isAbsolute = Uri.TryCreate(uriString: endpoint, uriKind: UriKind.Absolute, result: out var uri);
            var isOrigin = isAbsolute && TestHttpOrigin(uri);
            if (!isOrigin)
            {
                var message = "Recorder endpoint must be an absolute HTTP(S) origin.";
                throw new ArgumentException(message, paramName: nameof(recorder));
            }

            // Store the origin without a trailing slash so reservations and hub URLs compare consistently.
            recorder.BaseUrl = uri.GetLeftPart(part: UriPartial.Authority).TrimEnd(trimChar: '/');

            // Only the two capture families supported by the existing start/stop commands participate in recording.
            var driver = recorder.DriverName;
            var isChromium = driver.Contains(value: "chrome", comparisonType: StringComparison.OrdinalIgnoreCase);
            var isUia = driver.Equals(value: "UiaDriver", comparisonType: StringComparison.OrdinalIgnoreCase);
            if (!isChromium && !isUia)
            {
                var message = "Recorder driver must be ChromeDriver or UiaDriver.";
                throw new ArgumentException(message, paramName: nameof(recorder));
            }

            // Chromium has no UIA capture modes, so it always records in Standard mode.
            recorder.Mode = isChromium ? CaptureModes.Standard : recorder.Mode;
            var isModeSupported = recorder.Mode == CaptureModes.Standard
                || recorder.Mode == CaptureModes.User32
                || recorder.Mode == CaptureModes.Coordinate;
            if (!isModeSupported)
            {
                var message = "Recorder mode must be standard, user32, or coordinate.";
                throw new ArgumentException(message, paramName: nameof(recorder));
            }

            // Preserve no-op legacy script configurations but never silently skip executable or exported scripts.
            var isPreScriptEnabled = TestScriptEnabled(script: recorder.PreScript);
            var isAnyScriptEnabled = isPreScriptEnabled || TestScriptEnabled(script: recorder.PostScript);
            if (isAnyScriptEnabled)
            {
                var message = "Pre/post scripts are deferred to a later layer; disable their flags.";
                throw new ArgumentException(message, paramName: nameof(recorder));
            }

            // Default absent think-time settings so the automation builder reads them without null checks.
            recorder.ThinkTimeSettings ??= new ThinkTimeModel();

            // Reject negative bounds always, and an inverted range only when pacing is enabled.
            var timing = recorder.ThinkTimeSettings;
            var isNegative = timing.MinThinkTime < 0 || timing.MaxThinkTime < 0;
            var isRangeInverted = timing.Enabled && timing.MaxThinkTime < timing.MinThinkTime;
            if (isNegative || isRangeInverted)
            {
                var message = "Think-time values must be nonnegative with maximum >= minimum.";
                throw new ArgumentException(message, paramName: nameof(recorder));
            }
        }

        // Suspends or resumes one recorder of a recording session, matching its endpoint after normalization.
        // Throws KeyNotFoundException for an unknown session, InvalidOperationException when the session is not
        // recording, and ArgumentException when the endpoint is not part of the session.
        private static RecordingStatusModel SetRecorderSuspended(
            RecordingsRepository instance,
            Guid id,
            string baseUrl,
            bool isSuspended)
        {
            // Normalize the endpoint the same way start does, so callers can pass the URL they configured.
            var session = GetSession(instance, id);
            var isAbsolute = Uri.TryCreate(uriString: baseUrl, uriKind: UriKind.Absolute, result: out var uri);
            var endpoint = isAbsolute ? uri.GetLeftPart(part: UriPartial.Authority).TrimEnd(trimChar: '/') : baseUrl;

            // Apply the change under the session lock so it cannot race stop freezing the buffers.
            lock (session.SyncRoot)
            {
                // Only a recording session has live buffers to pause.
                if (session.State != RecordingStates.Recording)
                {
                    var message = $"The recording session is {session.State}; only a recording session can be paused.";
                    throw new InvalidOperationException(message);
                }

                // Reject an endpoint the session does not own instead of silently ignoring the request.
                var connection = session.Connections.FirstOrDefault(predicate: item => string.Equals(
                    a: item.Options.BaseUrl,
                    b: endpoint,
                    comparisonType: StringComparison.OrdinalIgnoreCase));
                if (connection is null)
                {
                    var message = $"The recorder '{baseUrl}' is not part of this recording session.";
                    throw new ArgumentException(message, paramName: nameof(baseUrl));
                }

                // Toggle buffering on the owned connection; the transport and any browser stay open.
                if (isSuspended)
                {
                    connection.Suspend();
                }
                else
                {
                    connection.Resume();
                }
            }

            // Report the new state through the regular status snapshot.
            return instance.GetStatus(id);
        }

        // Accepts only scheme, host, and port, because the hub route is appended to the origin verbatim.
        // Paths, queries, fragments, and embedded credentials corrupt that route or expose secrets in status.
        private static bool TestHttpOrigin(Uri uri)
        {
            // Require every origin condition together so one disallowed component rejects the endpoint.
            var isHttpScheme = uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
            var isBareOrigin = uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0;
            var isCredentialFree = uri.UserInfo.Length == 0;
            return isHttpScheme && isBareOrigin && isCredentialFree;
        }

        // Flags are independent in the extension, so either execution or flow export requires the later script layer.
        // Absent scripts count as disabled.
        private static bool TestScriptEnabled(ScriptModel script) =>
            script is not null && (script.Enabled || script.AddToAutomationFlow);

        #endregion

        #region *** Nested Types ***

        // Contains only process-local ownership; callers receive detached JSON and status models.
        private sealed class RecordingSession(RecordingAuthenticationModel authentication, RecorderConnection[] connections)
        {
            #region *** Properties   ***

            // Captures authentication as it existed at start; exported into the flow and never exposed in status.
            internal RecordingAuthenticationModel Authentication { get; } = authentication;

            // The exported flow is published once while holding SyncRoot; null until stop or for an empty recording.
            internal JsonObject Automation { get; set; }

            // Connections remain stable for the lifetime of a session, including after stop.
            internal RecorderConnection[] Connections { get; } = connections;

            // Processes whose windows are dropped from the flow, copied at start and applied at stop.
            internal int[] ExcludedProcessIds { get; init; } = [];

            // Window-title fragments whose windows are dropped from the flow, copied at start and applied at stop.
            internal string[] ExcludedWindowTitles { get; init; } = [];

            // Serializes asynchronous startup and stop for this session only.
            internal SemaphoreSlim Gate { get; } = new(initialCount: 1, maxCount: 1);

            // Identifies the retained session independently from recorder process identifiers.
            internal Guid Id { get; } = Guid.NewGuid();

            // The recorder of each generated job, published with the flow while holding SyncRoot; empty until stop.
            internal RecordingJobModel[] Jobs { get; set; } = [];

            // Reads and writes of lifecycle publication share this lock.
            internal object SyncRoot { get; } = new();

            // State starts before I/O and reaches stopped only after connection disposal.
            internal string State { get; set; } = RecordingStates.Starting;

            #endregion
        }

        #endregion
    }
}
