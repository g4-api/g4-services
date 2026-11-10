# Bots API

Register bots, read their status, test their connection and stop their monitors. All routes start with `/api/v4/g4/` and need no token header. See [tokens](../install-and-run/04-tokens.md) for what the Hub checks.

Base route: `/api/v4/g4/bots`

## Actions at a glance

| Verb | Route | Summary |
| --- | --- | --- |
| DELETE | `/api/v4/g4/bots/disconnect/all` | Stop monitors for all bots |
| DELETE | `/api/v4/g4/bots/disconnect` | Stop monitors for multiple bots |
| DELETE | `/api/v4/g4/bots/disconnect/{connection}` | Stop monitor for a specific bot |
| GET | `/api/v4/g4/bots/status` | Get the status of all connected bots |
| GET | `/api/v4/g4/bots/status/{id}` | Get the status of a specific bot |
| POST | `/api/v4/g4/bots/status` | Get status for multiple bots |
| POST | `/api/v4/g4/bots/register` | Register a new bot |
| DELETE | `/api/v4/g4/bots/register/{id}` | Unregister a single bot |
| DELETE | `/api/v4/g4/bots/register` | Batch unregister bots |
| DELETE | `/api/v4/g4/bots/register/all` | Batch unregister all disconnected bots |
| PUT | `/api/v4/g4/bots/register/{id}` | Update bot metadata and status |
| GET | `/api/v4/g4/bots/test/all` | Test connectivity for all bots |
| POST | `/api/v4/g4/bots/test` | Test connectivity for specific bots |
| GET | `/api/v4/g4/bots/test/{id}` | Test connectivity for a specific bot |

## Actions

### DELETE /api/v4/g4/bots/disconnect/all

| Item | Value |
| --- | --- |
| Swagger summary | "Stop monitors for all bots" |
| Request body | none |
| Responses | 204 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/bots/disconnect/all"
```

### DELETE /api/v4/g4/bots/disconnect

| Item | Value |
| --- | --- |
| Swagger summary | "Stop monitors for multiple bots" |
| Request body | `string[]` |
| Responses | 204 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/bots/disconnect" \
    -H "Content-Type: application/json" \
    -d '["connection-id-1", "connection-id-2"]'
```

### DELETE /api/v4/g4/bots/disconnect/{connection}

| Item | Value |
| --- | --- |
| Swagger summary | "Stop monitor for a specific bot" |
| Request body | none |
| Responses | 204 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/bots/disconnect/connection-id-1"
```

### GET /api/v4/g4/bots/status

| Item | Value |
| --- | --- |
| Swagger summary | "Get the status of all connected bots" |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/bots/status"
```

### GET /api/v4/g4/bots/status/{id}

| Item | Value |
| --- | --- |
| Swagger summary | "Get the status of a specific bot" |
| Request body | none |
| Responses | 200, 404 |

```bash
curl "http://localhost:9944/api/v4/g4/bots/status/bot-1234"
```

### POST /api/v4/g4/bots/status

| Item | Value |
| --- | --- |
| Swagger summary | "Get status for multiple bots" |
| Request body | `string[]` |
| Responses | 200 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/bots/status" \
    -H "Content-Type: application/json" \
    -d '["bot-1234", "bot-5678"]'
```

### POST /api/v4/g4/bots/register

| Item | Value |
| --- | --- |
| Swagger summary | "Register a new bot" |
| Request body | `ConnectedBotModel` |
| Responses | 200, 400 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/bots/register" \
    -H "Content-Type: application/json" \
    -d '{"id": "bot-1234", "name": "bot-1234", "type": "g4-bot", "machine": "WORKSTATION-01", "osVersion": "Windows 11", "callbackUri": "http://localhost:9966/api/v4/g4/bot"}'
```

### DELETE /api/v4/g4/bots/register/{id}

| Item | Value |
| --- | --- |
| Swagger summary | "Unregister a single bot" |
| Request body | none |
| Responses | 200, 404, 409, 502 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/bots/register/bot-1234"
```

### DELETE /api/v4/g4/bots/register

| Item | Value |
| --- | --- |
| Swagger summary | "Batch unregister bots" |
| Request body | `string[]` |
| Responses | 200, 400 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/bots/register" \
    -H "Content-Type: application/json" \
    -d '["bot-1234", "bot-5678"]'
```

### DELETE /api/v4/g4/bots/register/all

| Item | Value |
| --- | --- |
| Swagger summary | "Batch unregister all disconnected bots" |
| Request body | none |
| Responses | 200 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/bots/register/all"
```

### PUT /api/v4/g4/bots/register/{id}

| Item | Value |
| --- | --- |
| Swagger summary | "Update bot metadata and status" |
| Request body | `ConnectedBotModel` |
| Responses | 200, 400, 404 |

```bash
curl -X PUT "http://localhost:9944/api/v4/g4/bots/register/bot-1234" \
    -H "Content-Type: application/json" \
    -d '{"id": "bot-1234", "name": "bot-1234", "type": "g4-bot", "machine": "WORKSTATION-01", "osVersion": "Windows 11", "callbackUri": "http://localhost:9966/api/v4/g4/bot"}'
```

### GET /api/v4/g4/bots/test/all

| Item | Value |
| --- | --- |
| Swagger summary | "Test connectivity for all bots" |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/bots/test/all"
```

### POST /api/v4/g4/bots/test

| Item | Value |
| --- | --- |
| Swagger summary | "Test connectivity for specific bots" |
| Request body | `string[]` |
| Responses | 200 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/bots/test" \
    -H "Content-Type: application/json" \
    -d '["bot-1234", "bot-5678"]'
```

### GET /api/v4/g4/bots/test/{id}

| Item | Value |
| --- | --- |
| Swagger summary | "Test connectivity for a specific bot" |
| Request body | none |
| Responses | 200, 404 |

```bash
curl "http://localhost:9944/api/v4/g4/bots/test/bot-1234"
```
