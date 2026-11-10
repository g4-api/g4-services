# Module 8: Refresh the catalog

[⬅ Back to overview](README.md) · [⬅ Module 7](07-resolve-a-locator.md)

Where you are: the last module. About 2 minutes.

The Hub keeps a cached catalog of tools built from the plugin cache. When you add a plugin or register a capability, tell the Hub to rebuild it.

## What you'll do

- Call the sync route
- Know when to call it
- Re-read the tools in your client

## Steps

1. Call the route. It is a `GET` on the Hub, next to the MCP endpoint:

   ```text
   GET http://localhost:9944/api/v4/g4/mcp/sync
   ```

   The Swagger summary is "Sync the MCP tool catalog". It "Refreshes the cached tool definitions so that connected MCP agents have the most up-to-date list of tools."

2. Read the answer. A success is `204 No Content`, with no body. A failure is `500`, with an error object.

3. Know when to call it. Call it after you add a plugin or register a capability (see [Module 6](06-flows-as-tools.md)), and whenever discovery tools do not show something you just added. The catalog also follows cache changes on its own, so you may never need the route. TODO(verify: live): how fast the automatic update is was not measured.

4. Re-read in your client. The server cannot push `tools/list_changed` ([Module 1](01-endpoint-and-transport.md)), so ask again. Run `tools/list`, or call `g4.ResolveTools` again.

## Check your work

- `GET /api/v4/g4/mcp/sync` returns `204`.
- A discovery call now finds the plugin or capability that you added.

TODO(verify: live): no Hub was running, so the `204` was not seen.

## Where the facts come from

| Fact | Source |
| --- | --- |
| Route, summary, description and the `204` and `500` answers | `src\G4.Services.Hub.Api.V4\Controllers\McpController.cs:25-45` |
| Sync rebuilds the catalog from the plugin cache | `McpController.cs:40-45` |
| The catalog updates itself when the cache changes | `src\G4.Services.Domain.V4\Repositories\ToolsRepository.cs:100, 530` (`CacheChanged` handler) |
| Request sample | `http\G4.Services.Mcp.http` (last request) |

You have reached the end of the path. Back to the [Hub documentation](../README.md), or see [Troubleshooting](troubleshooting.md).
