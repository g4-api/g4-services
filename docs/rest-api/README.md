# Hub REST API Guide

Every action of the G4 Hub REST API, one page per controller. Each page lists the verb, the route, the Swagger summary, the request body model, the response codes and one curl example.

## Before you start

- All routes start with `/api/v4/g4/`. The examples use `http://localhost:9944`, the Hub default port.
- The Hub has no authentication middleware. See [tokens](../install-and-run/04-tokens.md).
- The Hub serves the live Swagger UI at `/swagger`. It is the authority when this guide and the code differ.
- Sample values such as `bot-1234`, `MyTeam` and `ExternalRepository` are placeholders. Replace them with your own.
- `automation.json` and `template.json` in the examples are files you provide.

## Pages

| Page | Base route | What it covers | Actions |
| --- | --- | --- | --- |
| [Automation](automation.md) | `/api/v4/g4/automation` | Invoke, initialize, resolve and stop automations | 6 |
| [Automation Async](automation-async.md) | `/api/v4/g4/automation/async` | Queue automations and read completed responses | 3 |
| [Flows](flows.md) | `/api/v4/g4/flows` | Register, list, invoke and remove flows | 10 |
| [Environments](environments.md) | `/api/v4/g4/environments` | Environments and their parameters | 9 |
| [Templates](templates.md) | `/api/v4/g4/templates` | Register, list and remove templates | 5 |
| [Integration](integration.md) | `/api/v4/g4/integration` | Plugin cache, manifests, documents and cache sync | 25 |
| [Credentials](credentials.md) | `/api/v4/g4/credentials` | Stored and OAuth credentials | 5 |
| [Bots](bots.md) | `/api/v4/g4/bots` | Bot registration, status and connection tests | 14 |
| [OpenAI Proxy](openai-proxy.md) | `/api/v4/g4/openaiproxy` | Chat completions through the OpenAI-compatible backend | 5 |
| [Recordings](recordings.md) | `/api/v4/g4/recordings` | Recording sessions. The guide is [Recording Automations](../recordings-api.md) | 6 |
| [MCP](mcp.md) | `/api/v4/g4/mcp` | MCP tool catalog sync. The MCP endpoint itself is served at `/api/v4/g4/mcp` | 1 |

## The ping route

`GET /api/v4/g4/ping` is served by `PingController` in the Hub project. Its Swagger summary is "Service metadata endpoint". Use it to check that the Hub is up.

```bash
curl "http://localhost:9944/api/v4/g4/ping"
```

## Ready-made requests

| File | What it holds |
| --- | --- |
| `http/G4.Services.Hub.http` | Requests for the REST Client extension of VS Code |
| `http/G4.Services.Recordings.http` | The recording requests |
| `http/G4.Services.Mcp.http` | The MCP requests |
| `postman/G4.Services.json` | The Postman collection |
