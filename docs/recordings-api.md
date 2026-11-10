# Recording Automations

The Hub connects to running G4 recorder services and turns their events into one native G4 automation flow.
The usual recorder endpoints are UIA on port `9955` and Chromium on port `9956`.
Recording sessions live in memory until the Hub stops; they are not persisted.

| Route | Result |
| --- | --- |
| `POST /api/v4/g4/recordings` | Starts the enabled recorders and returns the session ID and recorder diagnostics. |
| `GET /api/v4/g4/recordings/{id}` | Returns the lifecycle state and each recorder's connection, event count, suspension, and errors. After stop, `jobs` maps each job to its recorder. |
| `POST /api/v4/g4/recordings/{id}/suspend` | Drops one recorder's events until resumed; the body is `{ "baseUrl": "..." }`. |
| `POST /api/v4/g4/recordings/{id}/resume` | Resumes buffering for a suspended recorder; the body is `{ "baseUrl": "..." }`. |
| `POST /api/v4/g4/recordings/{id}/stop` | Stops the session and returns the automation; a repeated stop returns the same result. |
| `GET /api/v4/g4/recordings/{id}/flow` | Returns the completed automation without a metadata wrapper. |

The start body can also carry `excludedProcessIds` and `excludedWindowTitles`.
UIA events from those processes, or from windows whose title contains one of those fragments, are left out of the
automation.
A client that records from inside its own window, such as the VS Code recorder panel, uses them to skip its own
clicks.

An empty recording returns `204 No Content` from stop and flow retrieval.
Retrieval before stop returns `409 Conflict`, and an unknown ID returns `404 Not Found`.
Invalid settings, and suspending a recorder that is not part of the session, return `400 Bad Request`.
Suspending or resuming a session that is not recording returns `409 Conflict`.
When every recorder fails to start, start returns `502 Bad Gateway`; a partial start returns the session with
per-recorder errors.
The recordings HTTP examples cover the complete lifecycle.
