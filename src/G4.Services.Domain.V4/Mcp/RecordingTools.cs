using G4.Services.Domain.V4.Recordings;
using G4.Services.Domain.V4.Repositories;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Mcp
{
    /// <summary>
    /// Exposes the shared recording lifecycle to MCP clients without allocating separate recording state.
    /// </summary>
    /// <remarks>
    /// The SDK discovers these tools through <see cref="McpServerToolTypeAttribute"/> and lists them together with
    /// <see cref="G4SystemTools"/>; both types are registered from <see cref="McpToolTypes"/>. Session identifiers
    /// are shared with the REST recordings controller, so either transport can stop or retrieve a session the other
    /// started.
    /// </remarks>
    /// <param name="recordings">Shared REST/MCP session owner; disposal belongs to the application host.</param>
    [McpServerToolType]
    public sealed class RecordingTools(IRecordingsRepository recordings)
    {
        #region *** Methods      ***

        /// <summary>
        /// Returns a completed native automation, or JSON null when the stopped recording contains no events.
        /// </summary>
        /// <param name="id">Session identifier returned by either REST or MCP start.</param>
        /// <returns>
        /// The automation as JSON text and structured content, or an MCP tool error for an unknown or active recording.
        /// </returns>
        [McpServerTool(Name = "g4.GetAutomation", Title = "Get Recorded Automation", ReadOnly = true)]
        [Description("Get the native G4 automation after stopping a recording. Empty recordings return JSON null.")]
        public async Task<CallToolResult> GetAutomationAsync([Description("Recording session ID.")] Guid id)
        {
            // Reuse the REST lookup rules so active and unknown sessions produce the same domain failure.
            return await McpResults
                .InvokeAsync(operation: () => Task.FromResult<object>(result: recordings.GetAutomation(id)))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Returns lifecycle state and per-recorder diagnostics for a session created through either transport.
        /// </summary>
        /// <param name="id">Session identifier returned by either REST or MCP start.</param>
        /// <returns>
        /// A detached status as JSON text and structured content, or an MCP tool error for an unknown session.
        /// </returns>
        [McpServerTool(Name = "g4.GetRecordingStatus", Title = "Get Recording Status", ReadOnly = true)]
        [Description("Get recording state, connected recorders, event counts, and errors.")]
        public async Task<CallToolResult> GetRecordingStatusAsync([Description("Recording session ID.")] Guid id)
        {
            // Poll the shared session owner; the status model never exposes session authentication.
            return await McpResults
                .InvokeAsync(operation: () => Task.FromResult<object>(result: recordings.GetStatus(id)))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Starts all enabled recorders in the supplied settings and returns their shared session identifier.
        /// </summary>
        /// <param name="settings">REST-compatible start body: G4 authentication and recorder settings.</param>
        /// <param name="cancellationToken">Aborts startup and releases resources when the client disconnects.</param>
        /// <returns>
        /// Session diagnostics as JSON text and structured content; expected configuration and startup failures become tool errors.
        /// </returns>
        [McpServerTool(Name = "g4.StartRecording", Title = "Start Recording", Destructive = false, Idempotent = false)]
        [Description("Start enabled UIA and Chromium recorders. Recorder services must already be running. " +
            "Accepts the same settings body as the REST recordings endpoint; returns the session ID and diagnostics.")]
        public async Task<CallToolResult> StartRecordingAsync(
            [Description("REST start body: authentication and recorders.")] RecordingSettingsModel settings,
            CancellationToken cancellationToken)
        {
            // Preserve cancellation and partial-start cleanup owned by the shared repository.
            return await McpResults
                .InvokeAsync(operation: async () => await recordings.StartAsync(settings, cancellationToken).ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Stops owned recorders and returns the retained automation; cleanup survives caller cancellation.
        /// </summary>
        /// <param name="id">Session identifier returned by either REST or MCP start.</param>
        /// <returns>
        /// Native automation as JSON text and structured content, JSON null for an empty capture, or an unknown-session tool error.
        /// </returns>
        [McpServerTool(Name = "g4.StopRecording", Title = "Stop Recording", Destructive = true, Idempotent = true)]
        [Description("Stop recording, close owned Chromium browsers, and return the native G4 automation. " +
            "Repeated stop returns the same flow; empty recordings return JSON null.")]
        public async Task<CallToolResult> StopRecordingAsync([Description("Recording session ID.")] Guid id)
        {
            // Let the repository finish cleanup even when the MCP client disconnects during stop.
            return await McpResults
                .InvokeAsync(operation: async () => await recordings.StopAsync(id).ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        #endregion
    }
}
