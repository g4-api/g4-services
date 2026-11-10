# MCP API

Synchronize the MCP tool catalog. All routes start with `/api/v4/g4/` and need no token header. See [tokens](../install-and-run/04-tokens.md) for what the Hub checks.

Base route: `/api/v4/g4/mcp`

## Actions at a glance

| Verb | Route | Summary |
| --- | --- | --- |
| GET | `/api/v4/g4/mcp/sync` | Sync the MCP tool catalog |

## Actions

### GET /api/v4/g4/mcp/sync

| Item | Value |
| --- | --- |
| Swagger summary | "Sync the MCP tool catalog" |
| Request body | none |
| Responses | 204, 500 |

```bash
curl "http://localhost:9944/api/v4/g4/mcp/sync"
```
