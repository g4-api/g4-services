using System.Text.Json.Nodes;

namespace G4.Services.Domain.V4.Recordings
{
    /// <summary>
    /// Carries the automation generated from one stopped recording together with its job-to-recorder map.
    /// </summary>
    /// <remarks>
    /// The flow itself carries no recorder endpoints, so the map is returned beside it rather than inside it, keeping
    /// the exported automation unchanged for the designer and for MCP callers.
    /// </remarks>
    /// <param name="Automation">The generated flow, or null when the recording retained no playable events.</param>
    /// <param name="Jobs">The recorder of each job, in job order; empty for an empty recording.</param>
    internal sealed record RecordedAutomation(JsonObject Automation, RecordingJobModel[] Jobs);
}
