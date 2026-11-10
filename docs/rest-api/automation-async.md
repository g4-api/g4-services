# Automation Async API

Queue an automation for a Worker and read the completed responses later. All routes start with `/api/v4/g4/` and need no token header. See [tokens](../install-and-run/04-tokens.md) for what the Hub checks.

Base route: `/api/v4/g4/automation/async`

`automation.json` in the examples is a `G4AutomationModel`. See [what an automation is](../../../g4-engine/docs/understand-a-g4-automation/01-what-an-automation-is.md) for a complete minimal sample.

A Worker picks up the queued task. See [Install and Run the G4 Hub](../install-and-run/README.md) for how to start a Worker.

## Actions at a glance

| Verb | Route | Summary |
| --- | --- | --- |
| GET | `/api/v4/g4/automation/async/completed` | Retrieve completed automation responses. |
| POST | `/api/v4/g4/automation/async/start` | Enqueue a new automation task |
| POST | `/api/v4/g4/automation/async/file/start` | Enqueue a new automation task from a file. |

## Actions

### GET /api/v4/g4/automation/async/completed

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieve completed automation responses." |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/automation/async/completed"
```

### POST /api/v4/g4/automation/async/start

| Item | Value |
| --- | --- |
| Swagger summary | "Enqueue a new automation task" |
| Request body | `G4AutomationModel` |
| Responses | 202, 400 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/automation/async/start" \
    -H "Content-Type: application/json" \
    -d @automation.json
```

### POST /api/v4/g4/automation/async/file/start

| Item | Value |
| --- | --- |
| Swagger summary | "Enqueue a new automation task from a file." |
| Request body | `FileInvokeModel` |
| Responses | 202, 400 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/automation/async/file/start" \
    -H "Content-Type: application/json" \
    -d '{"path": "C:/automations/open-page.json"}'
```
