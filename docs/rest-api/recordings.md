# Recordings API

Start, control and stop recording sessions. All routes start with `/api/v4/g4/` and need no token header.

The curl examples and the full walk-through are in [Recording Automations](../recordings-api.md). The ready-made requests are in `http/G4.Services.Recordings.http`.

## Actions

| Verb | Route | Swagger summary | Request body | Responses |
| --- | --- | --- | --- | --- |
| GET | `/api/v4/g4/recordings/{id}/flow` | "Get the recorded automation" | none | 200, 204, 404, 409 |
| GET | `/api/v4/g4/recordings/{id}` | "Get recording status" | none | 200, 404 |
| POST | `/api/v4/g4/recordings/{id}/resume` | "Resume a recorder" | `RecorderSelectionModel` | 200, 400, 404, 409 |
| POST | `/api/v4/g4/recordings` | "Start recording" | `RecordingSettingsModel` | 201, 400, 409, 502 |
| POST | `/api/v4/g4/recordings/{id}/stop` | "Stop recording and get automation" | none | 200, 204, 404 |
| POST | `/api/v4/g4/recordings/{id}/suspend` | "Suspend a recorder" | `RecorderSelectionModel` | 200, 400, 404, 409 |
