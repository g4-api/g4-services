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
    /// started. Every tool name, title, and description is read from the nested <c>Catalog</c>, so the methods carry
    /// behavior only.
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
        [McpServerTool(Name = Catalog.GetAutomation.Name, Title = Catalog.GetAutomation.Title, ReadOnly = true)]
        [Description(Catalog.GetAutomation.Description)]
        public async Task<CallToolResult> GetAutomationAsync([Description(Catalog.GetAutomation.Arguments.Id)] Guid id)
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
        [McpServerTool(
            Name = Catalog.GetRecordingStatus.Name, Title = Catalog.GetRecordingStatus.Title, ReadOnly = true)]
        [Description(Catalog.GetRecordingStatus.Description)]
        public async Task<CallToolResult> GetRecordingStatusAsync(
            [Description(Catalog.GetRecordingStatus.Arguments.Id)] Guid id)
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
        [McpServerTool(
            Name = Catalog.StartRecording.Name,
            Title = Catalog.StartRecording.Title,
            Destructive = false,
            Idempotent = false)]
        [Description(Catalog.StartRecording.Description)]
        public async Task<CallToolResult> StartRecordingAsync(
            [Description(Catalog.StartRecording.Arguments.Settings)] RecordingSettingsModel settings,
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
        [McpServerTool(
            Name = Catalog.StopRecording.Name,
            Title = Catalog.StopRecording.Title,
            Destructive = true,
            Idempotent = true)]
        [Description(Catalog.StopRecording.Description)]
        public async Task<CallToolResult> StopRecordingAsync([Description(Catalog.StopRecording.Arguments.Id)] Guid id)
        {
            // Let the repository finish cleanup even when the MCP client disconnects during stop.
            return await McpResults
                .InvokeAsync(operation: async () => await recordings.StopAsync(id).ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        #endregion

        #region *** Nested Types ***

        // Holds the published wording of every recording tool, one nested class per tool in method order. Each tool
        // class exposes Name, Title, and Description, plus a nested Arguments class with one constant per argument
        // named after the argument in PascalCase. The text is wire contract: MCP clients receive it in tools/list,
        // g4.FindTool, and g4.FindTools, and agents choose and fill tools from it.
        private static class Catalog
        {
            #region *** Nested Types ***

            // Holds argument wording reused by several recording tools. Tool classes alias these constants instead of
            // copying the text, so one edit changes every tool that shares the argument.
            private static class Common
            {
                #region *** Constants    ***

                /// <summary>
                /// Describes the recording session identifier argument of every single-session tool.
                /// </summary>
                public const string Id = "Recording session ID.";

                #endregion
            }

            // Wording of the g4.GetAutomation tool.
            public static class GetAutomation
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.GetAutomation;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Get Recorded Automation";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Get the native G4 automation after stopping a recording. Empty " +
                    "recordings return JSON null.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the recording session identifier argument.
                    /// </summary>
                    public const string Id = Common.Id;

                    #endregion
                }

                #endregion
            }

            // Wording of the g4.GetRecordingStatus tool.
            public static class GetRecordingStatus
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.GetRecordingStatus;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Get Recording Status";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Get recording state, connected recorders, event counts, and errors.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the recording session identifier argument.
                    /// </summary>
                    public const string Id = Common.Id;

                    #endregion
                }

                #endregion
            }

            // Wording of the g4.StartRecording tool. Its cancellation token is supplied by the SDK, not by the client,
            // so it has no argument wording.
            public static class StartRecording
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.StartRecording;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Start Recording";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Start enabled UIA and Chromium recorders. Recorder services must " +
                    "already be running. Accepts the same settings body as the REST recordings endpoint; returns the " +
                    "session ID and diagnostics.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the recording settings argument.
                    /// </summary>
                    public const string Settings = "REST start body: authentication and recorders.";

                    #endregion
                }

                #endregion
            }

            // Wording of the g4.StopRecording tool.
            public static class StopRecording
            {
                #region *** Constants    ***

                /// <summary>
                /// The published tool name.
                /// </summary>
                public const string Name = G4ToolNames.StopRecording;

                /// <summary>
                /// The human-readable tool title.
                /// </summary>
                public const string Title = "Stop Recording";

                /// <summary>
                /// The tool description.
                /// </summary>
                public const string Description = "Stop recording, close owned Chromium browsers, and return the " +
                    "native G4 automation. Repeated stop returns the same flow; empty recordings return JSON null.";

                #endregion

                #region *** Nested Types ***

                // Argument wording, one constant per tool argument named after the argument in PascalCase.
                public static class Arguments
                {
                    #region *** Constants    ***

                    /// <summary>
                    /// Describes the recording session identifier argument.
                    /// </summary>
                    public const string Id = Common.Id;

                    #endregion
                }

                #endregion
            }

            #endregion
        }

        #endregion
    }
}
