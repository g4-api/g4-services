# Flows API

Register, list, invoke and remove flows. All routes start with `/api/v4/g4/` and need no token header. See [tokens](../install-and-run/04-tokens.md) for what the Hub checks.

Base route: `/api/v4/g4/flows`

The `automation` field of a flow is a Base64 string. The `base64 -w0` command in the examples is the GNU form; on macOS use `base64 -i automation.json`.

The `{namespace}` routes take any namespace. The routes without a namespace use `G4.System`.

## Actions at a glance

| Verb | Route | Summary |
| --- | --- | --- |
| PUT | `/api/v4/g4/flows` | Add or overwrite a flow |
| DELETE | `/api/v4/g4/flows` | Clear all flows |
| GET | `/api/v4/g4/flows/{key}` | Retrieve a G4.System flow manifest by key |
| GET | `/api/v4/g4/flows/{namespace}/{key}` | Retrieve a flow manifest by namespace and key |
| GET | `/api/v4/g4/flows` | Retrieve all flow manifests |
| POST | `/api/v4/g4/flows/{key}/invoke` | Invoke a G4.System flow by key |
| POST | `/api/v4/g4/flows/{namespace}/{key}/invoke` | Invoke a flow by namespace and key |
| DELETE | `/api/v4/g4/flows/{key}` | Remove a G4.System flow by key |
| DELETE | `/api/v4/g4/flows/{namespace}/{key}` | Remove a flow by namespace and key |
| DELETE | `/api/v4/g4/flows/invocations/{automationId}` | Stop a running flow invocation |

## Actions

### PUT /api/v4/g4/flows

| Item | Value |
| --- | --- |
| Swagger summary | "Add or overwrite a flow" |
| Request body | `G4FlowManifestModel` |
| Responses | 204, 400, 409 |

```bash
curl -X PUT "http://localhost:9944/api/v4/g4/flows" \
    -H "Content-Type: application/json" \
    -d "{\"key\": \"open-page\", \"namespace\": \"MyTeam\", \"automation\": \"$(base64 -w0 automation.json)\"}"
```

### DELETE /api/v4/g4/flows

| Item | Value |
| --- | --- |
| Swagger summary | "Clear all flows" |
| Request body | none |
| Responses | 204 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/flows"
```

### GET /api/v4/g4/flows/{key}

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieve a G4.System flow manifest by key" |
| Request body | none |
| Responses | 200, 404 |

```bash
curl "http://localhost:9944/api/v4/g4/flows/open-page"
```

### GET /api/v4/g4/flows/{namespace}/{key}

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieve a flow manifest by namespace and key" |
| Request body | none |
| Responses | 200, 404 |

```bash
curl "http://localhost:9944/api/v4/g4/flows/MyTeam/open-page"
```

### GET /api/v4/g4/flows

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieve all flow manifests" |
| Request body | none |
| Query | namespace (optional): returns only flows of this namespace. |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/flows?namespace=MyTeam"
```

### POST /api/v4/g4/flows/{key}/invoke

| Item | Value |
| --- | --- |
| Swagger summary | "Invoke a G4.System flow by key" |
| Request body | `G4FlowInvokeModel` |
| Responses | 200, 400, 404 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/flows/open-page/invoke" \
    -H "Content-Type: application/json" \
    -d '{"parameters": {"UserName": "admin"}, "referenceId": "my-run-1"}'
```

### POST /api/v4/g4/flows/{namespace}/{key}/invoke

| Item | Value |
| --- | --- |
| Swagger summary | "Invoke a flow by namespace and key" |
| Request body | `G4FlowInvokeModel` |
| Responses | 200, 400, 404 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/flows/MyTeam/open-page/invoke" \
    -H "Content-Type: application/json" \
    -d '{"parameters": {"UserName": "admin"}, "referenceId": "my-run-1"}'
```

### DELETE /api/v4/g4/flows/{key}

| Item | Value |
| --- | --- |
| Swagger summary | "Remove a G4.System flow by key" |
| Request body | none |
| Responses | 204, 404 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/flows/open-page"
```

### DELETE /api/v4/g4/flows/{namespace}/{key}

| Item | Value |
| --- | --- |
| Swagger summary | "Remove a flow by namespace and key" |
| Request body | none |
| Responses | 204, 404 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/flows/MyTeam/open-page"
```

### DELETE /api/v4/g4/flows/invocations/{automationId}

| Item | Value |
| --- | --- |
| Swagger summary | "Stop a running flow invocation" |
| Request body | none |
| Responses | 200 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/flows/invocations/my-run-1"
```
