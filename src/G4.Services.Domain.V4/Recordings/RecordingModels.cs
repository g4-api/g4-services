using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace G4.Services.Domain.V4.Recordings
{
    /// <summary>
    /// Supplies the G4 authentication and recorder settings used to start and export one recording session.
    /// </summary>
    public sealed class RecordingSettingsModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the G4 authentication exported unchanged into the generated automation.
        /// </summary>
        [Required]
        public RecordingAuthenticationModel Authentication { get; set; }

        /// <summary>
        /// Gets or sets the process identifiers whose windows are never recorded.
        /// </summary>
        /// <remarks>
        /// A client that records the desktop from inside its own window (for example the VS Code recorder panel)
        /// passes its process family so clicks and keys on its own chrome are dropped from the automation. A UIA
        /// event is excluded when its top window or any element in its chain belongs to one of these processes.
        /// Absent or empty excludes nothing; Chromium events carry no process identifiers and are never excluded.
        /// </remarks>
        public int[] ExcludedProcessIds { get; set; } = [];

        /// <summary>
        /// Gets or sets window-title fragments whose windows are never recorded.
        /// </summary>
        /// <remarks>
        /// A fallback for clients whose process identifiers cannot be resolved, such as the VS Code Extension
        /// Development Host. A UIA event is excluded when its top window name, or its locator when the top window is
        /// absent, contains one of these fragments, compared case-insensitively. Absent or empty excludes nothing.
        /// </remarks>
        public string[] ExcludedWindowTitles { get; set; } = [];

        /// <summary>
        /// Gets or sets the recorder endpoints; only enabled entries participate in the session.
        /// </summary>
        [Required]
        public RecorderModel[] Recorders { get; set; }

        #endregion
    }

    /// <summary>
    /// Identifies one recorder of a running session by its endpoint, for per-recorder operations such as suspend.
    /// </summary>
    public sealed class RecorderSelectionModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the recorder endpoint as configured at start; it is normalized before comparison.
        /// </summary>
        [Required]
        public string BaseUrl { get; set; }

        #endregion
    }

    /// <summary>
    /// Mirrors the G4 authentication contract carried into the exported automation.
    /// </summary>
    public sealed class RecordingAuthenticationModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the G4 service login password.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the G4 service login token.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Gets or sets the G4 service login name (the email used for registering).
        /// </summary>
        public string Username { get; set; }

        #endregion
    }

    /// <summary>
    /// Models recorder inputs while retaining arbitrary driver capabilities as a dictionary for flow export.
    /// </summary>
    public sealed class RecorderModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets an optional absolute endpoint; null selects the schema, host, and port fields instead.
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the complete playback driver configuration and Chromium launch capabilities.
        /// </summary>
        public Dictionary<string, object> DriverParameters { get; set; } = [];

        /// <summary>
        /// Gets or sets whether this recorder participates; omitted entries remain disabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the recorder host used when BaseUrl is absent.
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the UIA playback mode; Chromium uses standard mode.
        /// </summary>
        public string Mode { get; set; } = CaptureModes.Standard;

        /// <summary>
        /// Gets or sets the endpoint port as a JSON number or string; null is valid when BaseUrl supplies the origin.
        /// </summary>
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? Port { get; set; }

        /// <summary>
        /// Gets or sets optional post-script settings; null disables scripts, and active flags are rejected until script support exists.
        /// </summary>
        public ScriptModel PostScript { get; set; }

        /// <summary>
        /// Gets or sets optional pre-script settings; null disables scripts, and active flags are rejected until script support exists.
        /// </summary>
        public ScriptModel PreScript { get; set; }

        /// <summary>
        /// Gets or sets the HTTP scheme used when BaseUrl is absent.
        /// </summary>
        public string Schema { get; set; } = "http";

        /// <summary>
        /// Gets or sets per-job pacing; omission disables synthetic wait actions.
        /// </summary>
        public ThinkTimeModel ThinkTimeSettings { get; set; } = new();

        /// <summary>
        /// Gets or sets whether User32 clicks retain the element-relative pointer offset.
        /// </summary>
        public bool UseOffset { get; set; }

        // Resolves the driver name from either parsed JSON elements or in-memory strings; absent resolves to empty.
        internal string DriverName
        {
            get
            {
                // Treat absent parameters as an empty dictionary so a missing driver and a missing entry read alike.
                var parameters = DriverParameters ?? [];
                var isDriverPresent = parameters.TryGetValue(key: "driver", value: out var driver);

                // Convert JSON elements and strings uniformly; an absent or null driver resolves to an empty name.
                return isDriverPresent ? driver?.ToString() ?? "" : "";
            }
        }

        #endregion
    }

    /// <summary>
    /// Configures a recorder's pre- or post-script flags; either flag requires the deferred script layer.
    /// </summary>
    public sealed class ScriptModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets whether the script is exported into the generated automation flow.
        /// </summary>
        public bool AddToAutomationFlow { get; set; }

        /// <summary>
        /// Gets or sets whether the script executes around the recording session.
        /// </summary>
        public bool Enabled { get; set; }

        #endregion
    }

    /// <summary>
    /// Configures the existing threshold-and-cap think-time behavior in milliseconds.
    /// </summary>
    public sealed class ThinkTimeModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets whether waits are inserted between resolved actions within each job.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the maximum inserted wait duration in milliseconds.
        /// </summary>
        public long MaxThinkTime { get; set; }

        /// <summary>
        /// Gets or sets the threshold in milliseconds that a recorded gap must exceed to produce a wait.
        /// </summary>
        public long MinThinkTime { get; set; }

        #endregion
    }

    /// <summary>
    /// Provides a session identifier and per-recorder diagnostics without exposing authentication credentials.
    /// </summary>
    public sealed class RecordingStatusModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets whether stop produced a nonempty automation available from the flow endpoint.
        /// </summary>
        public bool HasAutomation { get; set; }

        /// <summary>
        /// Gets or sets the identifier used by stop, status, and flow requests.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the recorder that produced each job of the generated automation, in job order.
        /// </summary>
        /// <remarks>
        /// Empty until stop, and for an empty recording. The flow itself carries no endpoint, so clients that
        /// post-process jobs per recorder (for example inserting a recorder's pre- and post-scripts) read this map.
        /// </remarks>
        public RecordingJobModel[] Jobs { get; set; } = [];

        /// <summary>
        /// Gets or sets a detached status snapshot for every enabled endpoint.
        /// </summary>
        public RecorderStatusModel[] Recorders { get; set; } = [];

        /// <summary>
        /// Gets or sets the lifecycle state: starting, recording, stopping, or stopped.
        /// </summary>
        public string State { get; set; } = RecordingStates.Starting;

        #endregion
    }

    /// <summary>
    /// Reports connection availability, retained event count, and lifecycle errors for one recorder.
    /// </summary>
    public sealed class RecorderStatusModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the normalized endpoint reserved by the session.
        /// </summary>
        public string BaseUrl { get; set; } = "";

        /// <summary>
        /// Gets or sets the Chromium process identifier, or null for UIA and after successful cleanup.
        /// </summary>
        public int? BrowserProcessId { get; set; }

        /// <summary>
        /// Gets or sets whether the capture transport is connected at the time of the status request.
        /// </summary>
        public bool Connected { get; set; }

        /// <summary>
        /// Gets or sets the latest startup or cleanup error, or null when none occurred.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Gets or sets the number of buffered event envelopes retained for conversion.
        /// </summary>
        public int EventCount { get; set; }

        /// <summary>
        /// Gets or sets whether the recorder is suspended; a suspended recorder's events are dropped until resumed.
        /// </summary>
        public bool Suspended { get; set; }

        #endregion
    }

    /// <summary>
    /// Links one job of the generated automation to the recorder endpoint whose events it holds.
    /// </summary>
    public sealed class RecordingJobModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the normalized endpoint of the recorder whose events the job holds.
        /// </summary>
        public string BaseUrl { get; set; } = "";

        /// <summary>
        /// Gets or sets the job reference identifier, as written in the job's <c>reference.id</c>.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Gets or sets the zero-based position of the job in the automation's only stage.
        /// </summary>
        /// <remarks>
        /// Always serialized: the Hub's JSON options omit default values, which would drop the first job's index 0
        /// and leave clients unable to place per-recorder content in that job.
        /// </remarks>
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int Index { get; set; }

        #endregion
    }

    // Shared mode values keep validation and UIA plugin selection aligned without introducing an enum contract.
    internal static class CaptureModes
    {
        #region *** Constants    ***

        // Coordinate mode emits physical input actions without element locators.
        internal const string Coordinate = "coordinate";

        // Standard mode emits element-pattern input actions.
        internal const string Standard = "standard";

        // User32 mode emits physical input actions with element locators.
        internal const string User32 = "user32";

        #endregion
    }

    // Session state values are serialized directly to REST clients.
    internal static class RecordingStates
    {
        #region *** Constants    ***

        // Starting sessions own endpoint reservations while connecting.
        internal const string Starting = "starting";

        // Recording sessions accept incoming event envelopes.
        internal const string Recording = "recording";

        // Stopping sessions have frozen their buffers and are releasing transports.
        internal const string Stopping = "stopping";

        // Stopped sessions retain their completed flow until the API process exits.
        internal const string Stopped = "stopped";

        #endregion
    }
}
