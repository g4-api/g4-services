# Automation API

Invoke an automation and wait for the result, resolve macros, or stop a running session. All routes start with `/api/v4/g4/` and need no token header. See [tokens](../install-and-run/04-tokens.md) for what the Hub checks.

Base route: `/api/v4/g4/automation`

`automation.json` in the examples is a `G4AutomationModel`. See [what an automation is](../../../g4-engine/docs/understand-a-g4-automation/01-what-an-automation-is.md) for a complete minimal sample.

## Actions at a glance

| Verb | Route | Summary |
| --- | --- | --- |
| POST | `/api/v4/g4/automation/file/invoke` | Invokes an automation session from a file. |
| POST | `/api/v4/g4/automation/invoke` | Invokes an automation session. |
| POST | `/api/v4/g4/automation/base64/invoke` | Invokes an automation session using Base64-encoded input. |
| POST | `/api/v4/g4/automation/init` | Initializes an automation request. |
| POST | `/api/v4/g4/automation/resolve` | Resolves macros in the provided G4AutomationModel. |
| GET | `/api/v4/g4/automation/stop/{automationId}` | Stops a running automation session. |

## Actions

### POST /api/v4/g4/automation/file/invoke

| Item | Value |
| --- | --- |
| Swagger summary | "Invokes an automation session from a file." |
| Request body | `FileInvokeModel` |
| Responses | 200, 400 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/automation/file/invoke" \
    -H "Content-Type: application/json" \
    -d '{"path": "C:/automations/open-page.json"}'
```

### POST /api/v4/g4/automation/invoke

| Item | Value |
| --- | --- |
| Swagger summary | "Invokes an automation session." |
| Request body | `G4AutomationModel` |
| Responses | 200, 400 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/automation/invoke" \
    -H "Content-Type: application/json" \
    -d @automation.json
```

### POST /api/v4/g4/automation/base64/invoke

| Item | Value |
| --- | --- |
| Swagger summary | "Invokes an automation session using Base64-encoded input." |
| Request body | `string` |
| Responses | 200, 400 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/automation/base64/invoke" \
    -H "Content-Type: application/json" \
    -d "\"$(base64 -w0 automation.json)\""
```

### POST /api/v4/g4/automation/init

| Item | Value |
| --- | --- |
| Swagger summary | "Initializes an automation request." |
| Request body | `G4AutomationModel` |
| Responses | 200 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/automation/init" \
    -H "Content-Type: application/json" \
    -d @automation.json
```

### POST /api/v4/g4/automation/resolve

| Item | Value |
| --- | --- |
| Swagger summary | "Resolves macros in the provided G4AutomationModel." |
| Request body | `G4AutomationModel` |
| Responses | 200 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/automation/resolve" \
    -H "Content-Type: application/json" \
    -d @automation.json
```

### GET /api/v4/g4/automation/stop/{automationId}

| Item | Value |
| --- | --- |
| Swagger summary | "Stops a running automation session." |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/automation/stop/my-run-1"
```
