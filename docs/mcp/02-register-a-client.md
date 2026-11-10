# Module 2: Register a client

[⬅ Back to overview](README.md) · [⬅ Module 1](01-endpoint-and-transport.md)

Where you are: you know the endpoint. About 7 minutes.

Now tell your AI client where the Hub is. Only the address matters. The shapes below differ per client.

## What you'll do

- Add the Hub to Claude Code
- Use the file that G4 Studio writes for VS Code
- Try Claude Desktop and Codex with unverified starting points
- Test the connection by hand

## Steps

1. Register in Claude Code. Run this command in a terminal, with your own address:

   ```text
   claude mcp add --transport http g4 http://localhost:9944/api/v4/g4/mcp
   ```

   The `--transport http` form with a name and a URL is the same one that another G4 project generates for its own gateway. TODO(verify: live): run the command against a running Hub and list the tools in Claude Code.

2. Register in VS Code. G4 Studio writes this file for you when you run `New-Project`. It is `src\.vscode\mcp.json` inside the new project:

   ```json
   {
       "servers": {
           "g4-engine": {
               "type": "http",
               "url": "http://localhost:9944/api/v4/g4/mcp"
           }
       },
       "inputs": []
   }
   ```

   Studio builds the URL from `g4Server` in the project `manifest.json`: scheme, host and port, then the fixed route `/api/v4/g4/mcp`. If you do not use Studio, create `.vscode\mcp.json` with the same content. The file is named `mcp.json` and sits in `.vscode`, not `.mcp.json`.

3. Try Claude Desktop. This client reads a JSON file. The Hub speaks HTTP only, so this client may need a bridge. TODO(verify: client): this snippet is an unverified starting point and no code in the G4 repositories confirms it. Check your client's documentation first:

   ```json
   {
       "mcpServers": {
           "g4": {
               "url": "http://localhost:9944/api/v4/g4/mcp"
           }
       }
   }
   ```

4. Try Codex. Codex reads `config.toml`. TODO(verify: client): this snippet is an unverified starting point. Check the Codex documentation for the key that sets a Streamable HTTP server:

   ```toml
   [mcp_servers.g4]
   url = "http://localhost:9944/api/v4/g4/mcp"
   ```

5. Test by hand, without any client. Send `initialize` with the sample from `http\G4.Services.Mcp.http`:

   ```json
   {
       "jsonrpc": "2.0",
       "id": 1,
       "method": "initialize",
       "params": {
           "protocolVersion": "2025-11-25",
           "capabilities": {},
           "clientInfo": { "name": "manual-client", "version": "1.0" }
       }
   }
   ```

   Post it to the endpoint with the three headers from [Module 1](01-endpoint-and-transport.md). The answer names the server `g4-engine-mcp`. Then send `notifications/initialized`, which answers `202`, and `tools/list`.

   TODO(verify: live): no Hub was running when this page was written, so the answers to these requests were not captured.

## Check your work

- Your client lists the G4 tools, 22 of them.
- A hand-made `initialize` request reaches the Hub and gets a JSON-RPC answer.
- If you use VS Code, the `g4-engine` server shows in the MCP server list.

## Where the facts come from

| Fact | Source |
| --- | --- |
| Request samples and headers | `http\G4.Services.Mcp.http:1-30` |
| Shape and file name that Studio writes | `g4-vscode-extension\src\commands\new-project.ts:609-620, 656-665` |
| Route used to build the URL | `new-project.ts:613` |
| Same `claude mcp add --transport http` form in another G4 project | `g4-litellm\src\G4.Gateway\client\src\features\mcp-servers\mcp-connect.ts:218` |

Next: [Module 3: Discover tools](03-discover-tools.md)
