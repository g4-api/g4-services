using G4.Models;
using G4.Services.Domain.V4;
using G4.Services.Domain.V4.Recordings;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/[controller]")]
    [SwaggerTag(description: "Starts and stops recording sessions against running G4 UIA and Chromium recorder services " +
        "and returns their native G4 automation flows. Sessions are shared with the MCP recording tools, so a session " +
        "started through one transport can be stopped or retrieved through the other.")]
    [ApiExplorerSettings(GroupName = "G4 Hub")]
    public class RecordingsController(IDomain domain) : ControllerBase
    {
        #region *** Fields       ***
        // The domain service whose recordings repository owns every in-memory recording session.
        private readonly IDomain _domain = domain;
        #endregion

        #region *** Methods      ***
        [HttpGet("{id:guid}/flow")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Get the recorded automation",
            description: "Returns the native G4 automation of a stopped recording without a metadata wrapper. Empty recordings return 204 No Content.",
            Tags = ["Recordings"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "The generated automation.", type: typeof(JsonObject), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "The recording contains no events.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Unknown recording session.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        [SwaggerResponse(StatusCodes.Status409Conflict, description: "The recording has not stopped yet.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        #endregion
        public IActionResult GetAutomation([SwaggerParameter(description: "The recording session identifier.", Required = true)] Guid id)
        {
            // Return native automation JSON directly so callers can save it or send it to G4 without unwrapping metadata.
            var automation = _domain.G4.Recordings.GetAutomation(id);

            // Distinguish an empty capture from a generated flow without inventing a synthetic automation.
            return automation is null ? NoContent() : Ok(value: automation);
        }

        [HttpGet("{id:guid}")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Get recording status",
            description: "Returns the lifecycle state, per-recorder diagnostics (connection, event count, suspension, errors), and after stop the recorder of each generated job, without authentication credentials.",
            Tags = ["Recordings"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "The current session status.", type: typeof(RecordingStatusModel), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Unknown recording session.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        #endregion
        public IActionResult GetStatus([SwaggerParameter(description: "The recording session identifier.", Required = true)] Guid id)
        {
            // A detached diagnostic model lets clients poll without accessing mutable session ownership.
            return Ok(value: _domain.G4.Recordings.GetStatus(id));
        }

        [HttpPost("{id:guid}/resume")]
        [Consumes(MediaTypeNames.Application.Json)]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Resume a recorder",
            description: "Resumes buffering for one suspended recorder of a running session. Events that arrived while it was suspended stay dropped.",
            Tags = ["Recordings"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "The session status with the resumed recorder.", type: typeof(RecordingStatusModel), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status400BadRequest, description: "The recorder is not part of the session.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Unknown recording session.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        [SwaggerResponse(StatusCodes.Status409Conflict, description: "The session is not recording.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        #endregion
        public IActionResult ResumeRecorder(
            [SwaggerParameter(description: "The recording session identifier.", Required = true)] Guid id,
            [FromBody][Required][SwaggerRequestBody(description: "The recorder to resume.", Required = true)] RecorderSelectionModel recorder)
        {
            // Resume through the shared repository so REST and MCP observe the same per-recorder state.
            return Ok(value: _domain.G4.Recordings.ResumeRecorder(id, recorder.BaseUrl));
        }

        [HttpPost]
        [Consumes(MediaTypeNames.Application.Json)]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Start recording",
            description: "Connects every enabled recorder and launches Chromium browsers before returning their outcomes. A partial start returns the session with per-recorder errors.",
            Tags = ["Recordings"])]
        [SwaggerResponse(StatusCodes.Status201Created, description: "Recording started.", type: typeof(RecordingStatusModel), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status400BadRequest, description: "Invalid configuration or an unsupported feature such as pre/post scripts with active flags.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        [SwaggerResponse(StatusCodes.Status409Conflict, description: "A requested recorder endpoint is already recording.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        [SwaggerResponse(StatusCodes.Status502BadGateway, description: "No recorder started; the detail lists every endpoint failure.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        #endregion
        public async Task<IActionResult> StartRecordingAsync(
            [FromBody][Required][SwaggerRequestBody(description: "The G4 authentication and recorder settings.", Required = true)] RecordingSettingsModel settings,
            CancellationToken cancellationToken)
        {
            // Wait for the basic lifecycle so the response reports real connection outcomes rather than a pending operation.
            var status = await _domain.G4.Recordings.StartAsync(settings, cancellationToken).ConfigureAwait(false);

            // Publish the status route so clients can observe the session without retaining the original settings.
            return CreatedAtAction(actionName: nameof(GetStatus), routeValues: new { id = status.Id }, value: status);
        }

        [HttpPost("{id:guid}/stop")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Stop recording and get automation",
            description: "Freezes received events, converts the flow, and closes owned recorders. Repeated stop returns the same flow; empty recordings return 204 No Content.",
            Tags = ["Recordings"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "The generated automation.", type: typeof(JsonObject), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status204NoContent, description: "The recording contains no events.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Unknown recording session.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        #endregion
        public async Task<IActionResult> StopRecordingAsync([SwaggerParameter(description: "The recording session identifier.", Required = true)] Guid id)
        {
            // Cleanup continues independently of request cancellation so an abandoned response does not leak browsers.
            var automation = await _domain.G4.Recordings.StopAsync(id).ConfigureAwait(false);

            // Distinguish an empty capture from a generated flow without inventing a synthetic automation.
            return automation is null ? NoContent() : Ok(value: automation);
        }

        [HttpPost("{id:guid}/suspend")]
        [Consumes(MediaTypeNames.Application.Json)]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Suspend a recorder",
            description: "Pauses buffering for one recorder of a running session; its events are dropped until resumed. The recorder stays connected.",
            Tags = ["Recordings"])]
        [SwaggerResponse(StatusCodes.Status200OK, description: "The session status with the suspended recorder.", type: typeof(RecordingStatusModel), contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status400BadRequest, description: "The recorder is not part of the session.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        [SwaggerResponse(StatusCodes.Status404NotFound, description: "Unknown recording session.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        [SwaggerResponse(StatusCodes.Status409Conflict, description: "The session is not recording.", type: typeof(GenericErrorModel), contentTypes: MediaTypeNames.Application.ProblemJson)]
        #endregion
        public IActionResult SuspendRecorder(
            [SwaggerParameter(description: "The recording session identifier.", Required = true)] Guid id,
            [FromBody][Required][SwaggerRequestBody(description: "The recorder to suspend.", Required = true)] RecorderSelectionModel recorder)
        {
            // Suspend through the shared repository so REST and MCP observe the same per-recorder state.
            return Ok(value: _domain.G4.Recordings.SuspendRecorder(id, recorder.BaseUrl));
        }
        #endregion
    }
}
