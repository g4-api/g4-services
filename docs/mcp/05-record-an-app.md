# Module 5: Record an app

[⬅ Back to overview](README.md) · [⬅ Module 4](04-drive-a-session.md)

Where you are: you can run rules. About 5 minutes.

The agent can also watch you work. The recording tools start the recorders, wait while you use the app, then return a G4 automation that you can store as a flow in the next module.

Recording shares its sessions with the REST API. A recording started here can be read through REST, and the other way round. See the [recordings guide](../recordings-api.md) for the REST side.

## What you'll do

- Start a recording
- Check its state
- Stop it and read the automation

## Steps

1. Start the recorders yourself first. The recorder services must already be running, for example the UIA recorder on 9955 and the Chromium recorder on 9956. The [recorders install path](../../../g4-recorders/docs/install-and-operate/README.md) shows how.

2. Start the recording with `g4.StartRecording`.

   Purpose, quoted: "Start enabled UIA and Chromium recorders. Recorder services must already be running. Accepts the same settings body as the REST recordings endpoint; returns the session ID and diagnostics."

   | Argument | Type | Required | Meaning |
   | --- | --- | --- | --- |
   | `settings` | object | Yes | The REST start body: `authentication` and `recorders` (both required). It may also carry `excludedProcessIds` and `excludedWindowTitles` to keep some windows out of the recording |

   Only recorders with `"enabled": true` take part. At least one is required. Without one the call fails with `At least one enabled recorder is required.`

   ```json
   {
       "jsonrpc": "2.0",
       "id": 30,
       "method": "tools/call",
       "params": {
           "name": "g4.StartRecording",
           "arguments": {
               "settings": {
                   "authentication": { "token": "<your-g4-token>" },
                   "recorders": [
                       {
                           "enabled": true,
                           "baseUrl": "http://localhost:9956",
                           "driverParameters": { "driver": "ChromeDriver" }
                       }
                   ]
               }
           }
       }
   }
   ```

   The answer holds the recording `id`. Keep it.

3. Check the state with `g4.GetRecordingStatus`.

   Purpose, quoted: "Get recording state, connected recorders, event counts, and errors."

   | Argument | Type | Required | Meaning |
   | --- | --- | --- | --- |
   | `id` | GUID string | Yes | The recording session id |

   The answer carries `id`, `state`, `hasAutomation`, `jobs` and `recorders`.

4. Do the work you want to record. Click through your app or browser.

5. Stop with `g4.StopRecording`.

   Purpose, quoted: "Stop recording, close owned Chromium browsers, and return the native G4 automation. Repeated stop returns the same flow; empty recordings return JSON null."

   It takes one required argument, `id`. Stopping twice is safe: the second call returns the same flow.

6. Read it again later with `g4.GetAutomation`.

   Purpose, quoted: "Get the native G4 automation after stopping a recording. Empty recordings return JSON null."

   It takes one required argument, `id`. If the recording has not stopped yet, the call fails with `Stop the recording before requesting its automation.` An unknown id fails with `The recording session was not found.`

## Check your work

- `g4.StartRecording` returned an `id`.
- `g4.GetRecordingStatus` showed the recorders you enabled.
- `g4.StopRecording` returned an automation object, or `null` if you recorded nothing.

TODO(verify: live): no Hub or recorder was running, so no answer was captured. The request samples are the ones in `http\G4.Services.Mcp.http`.

## Where the facts come from

| Fact | Source |
| --- | --- |
| Tool wording | `src\G4.Services.Domain.V4\Mcp\RecordingTools.cs`, nested `Catalog` class |
| `settings` is the REST body, and its required fields | `src\G4.Services.Domain.V4\Recordings\RecordingModels.cs:11-49` |
| Status fields | `RecordingModels.cs:220-235` |
| Enabled-recorder rule | `src\G4.Services.Domain.V4\Repositories\RecordingsRepository.cs:336-348` |
| Stop-before-read rule and unknown-session message | `RecordingsRepository.cs:80-95, 313-322` |
| Failure types that become `isError` | `src\G4.Services.Domain.V4\Mcp\McpResults.cs:82-89` |
| Request samples | `http\G4.Services.Mcp.http` (requests 3 to 6) |

Next: [Module 6: Flows as tools](06-flows-as-tools.md)
