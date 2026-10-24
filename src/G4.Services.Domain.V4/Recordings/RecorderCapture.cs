using System.Text.Json.Nodes;

namespace G4.Services.Domain.V4.Recordings
{
    /// <summary>
    /// Supplies one endpoint's frozen models to the compute-only builder and deterministic tests.
    /// </summary>
    /// <remarks>
    /// The positional record keeps the configuration and snapshot together during conversion.
    /// Its references are fixed after creation; callers own the already detached JSON nodes.
    /// </remarks>
    /// <param name="Options">Detached endpoint configuration used to select playback behavior.</param>
    /// <param name="Events">Frozen event copies ordered by arrival within this endpoint.</param>
    internal sealed record RecorderCapture(RecorderModel Options, JsonObject[] Events);
}
