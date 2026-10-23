using G4.Services.Domain.V4.Recordings;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Defines the recording-session lifecycle shared by the REST recordings controller and the MCP recording tools.
    /// </summary>
    /// <remarks>
    /// A session connects to one or more G4 UIA or Chromium recorder services, buffers their events in memory,
    /// and converts them into one native G4 automation when stopped. Session identifiers are transport-neutral,
    /// so a session started through REST can be stopped or retrieved through MCP and the reverse.
    /// Failures use ordinary exception types that both transports map to caller-visible errors:
    /// <see cref="ArgumentException"/> for invalid settings, <see cref="KeyNotFoundException"/> for unknown sessions,
    /// <see cref="InvalidOperationException"/> for lifecycle conflicts, and <see cref="HttpRequestException"/>
    /// when no recorder starts.
    /// </remarks>
    public interface IRecordingsRepository
    {
        #region *** Methods      ***

        /// <summary>
        /// Gets a detached flow after stop, returns null for an empty recording, and rejects premature retrieval.
        /// </summary>
        /// <param name="id">The session identifier returned by start.</param>
        /// <returns>A caller-owned G4 automation object, or null for a stopped empty session.</returns>
        /// <exception cref="KeyNotFoundException">The session identifier is unknown.</exception>
        /// <exception cref="InvalidOperationException">The session has not stopped yet.</exception>
        JsonObject GetAutomation(Guid id);

        /// <summary>
        /// Gets session diagnostics and each recorder's current connection and event count.
        /// </summary>
        /// <param name="id">The session identifier returned by start.</param>
        /// <returns>A credential-free status snapshot detached from the live session.</returns>
        /// <exception cref="KeyNotFoundException">The session identifier is unknown.</exception>
        RecordingStatusModel GetStatus(Guid id);

        /// <summary>
        /// Resumes buffering for one recorder of a running session after a suspend.
        /// </summary>
        /// <remarks>
        /// Events that arrived while the recorder was suspended stay dropped. Resuming an active recorder is a no-op.
        /// </remarks>
        /// <param name="id">The session identifier returned by start.</param>
        /// <param name="baseUrl">
        /// The recorder endpoint as configured at start; it is normalized before comparison.
        /// </param>
        /// <returns>A status snapshot that reflects the resumed recorder.</returns>
        /// <exception cref="KeyNotFoundException">The session identifier is unknown.</exception>
        /// <exception cref="ArgumentException">The endpoint is not part of the session.</exception>
        /// <exception cref="InvalidOperationException">The session is not recording.</exception>
        RecordingStatusModel ResumeRecorder(Guid id, string baseUrl);

        /// <summary>
        /// Starts enabled endpoints concurrently and returns their outcomes after connection and browser launch.
        /// </summary>
        /// <remarks>
        /// Settings are validated and copied before any network connection opens, and every requested endpoint is
        /// reserved atomically so two sessions never share one recorder broadcast. A partial start keeps the session
        /// and reports per-recorder errors; a start where no recorder connects releases all resources and throws.
        /// </remarks>
        /// <param name="settings">The authentication and recorder settings for this session.</param>
        /// <param name="cancellationToken">Cancels startup and releases resources when the caller disconnects.</param>
        /// <returns>Status for a session with at least one successfully started recorder.</returns>
        /// <exception cref="ArgumentException">The settings are missing, malformed, or use a deferred feature.</exception>
        /// <exception cref="InvalidOperationException">A requested endpoint is already recording.</exception>
        /// <exception cref="HttpRequestException">No recorder started; the message lists every endpoint failure.</exception>
        Task<RecordingStatusModel> StartAsync(RecordingSettingsModel settings, CancellationToken cancellationToken);

        /// <summary>
        /// Freezes received events, builds one chronological automation, and closes every owned connection once.
        /// </summary>
        /// <remarks>
        /// Repeated stop calls return the retained result without contacting the recorders again. Cleanup runs to
        /// completion independently of caller cancellation so an abandoned request never leaks a browser.
        /// </remarks>
        /// <param name="id">The session identifier returned by start.</param>
        /// <returns>The generated flow, or null when no events were retained.</returns>
        /// <exception cref="KeyNotFoundException">The session identifier is unknown.</exception>
        Task<JsonObject> StopAsync(Guid id);

        /// <summary>
        /// Suspends buffering for one recorder of a running session; its events are dropped until resumed.
        /// </summary>
        /// <remarks>
        /// The recorder stays connected, so resuming needs no reconnect or browser relaunch. Suspending an already
        /// suspended recorder is a no-op.
        /// </remarks>
        /// <param name="id">The session identifier returned by start.</param>
        /// <param name="baseUrl">
        /// The recorder endpoint as configured at start; it is normalized before comparison.
        /// </param>
        /// <returns>A status snapshot that reflects the suspended recorder.</returns>
        /// <exception cref="KeyNotFoundException">The session identifier is unknown.</exception>
        /// <exception cref="ArgumentException">The endpoint is not part of the session.</exception>
        /// <exception cref="InvalidOperationException">The session is not recording.</exception>
        RecordingStatusModel SuspendRecorder(Guid id, string baseUrl);

        #endregion
    }
}
