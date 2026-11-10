# Use G4 from an AI Agent (MCP)

This path shows you how to connect an AI agent to the G4 Hub over MCP, then discover tools, drive a browser session, record an app and run stored flows.

Plan for about 45 minutes.

---

## Who is this for?

Anyone who wants an AI agent such as Claude Code, Codex or VS Code to use G4 as a set of tools. You need a running Hub. If you do not have one yet, follow [Install and Run the G4 Hub](../install-and-run/README.md) first.

## What you'll do

By the end you will have:

- Understood what the MCP endpoint is and what it does not do
- Registered the Hub in an MCP client
- Discovered the G4 tools with the four discovery tools
- Driven a browser session with rules
- Recorded an app and turned the result into a stored flow
- Run, stopped and removed a flow from the agent
- Resolved a locator with an OpenAI model
- Refreshed the tool catalog after a plugin change

## Prerequisites

- A running G4 Hub, reachable at `http://localhost:9944` or your own address
- An MCP client that supports the Streamable HTTP transport
- A G4 token for the tools that start sessions or record

> **💡 Tip:** The exact wording of each tool comes from the Hub source. The `tools/list` request always returns the current list, so treat it as the final word if this path and the Hub ever differ.

---

## Modules

Work through these in order. Each one builds on the previous.

| # | Module | What you'll do | Time |
| --- | --- | --- | --- |
| 1 | [Endpoint and transport](01-endpoint-and-transport.md) | Learn the address, the transport and the limits | 5 min |
| 2 | [Register a client](02-register-a-client.md) | Add the Hub to Claude Code, VS Code, Claude Desktop or Codex | 7 min |
| 3 | [Discover tools](03-discover-tools.md) | Use `g4.ResolveTools`, `g4.FindTools`, `g4.FindTool` and `g4.FindExamples` | 6 min |
| 4 | [Drive a session](04-drive-a-session.md) | Start a session, send rules, read and clear the buffer | 8 min |
| 5 | [Record an app](05-record-an-app.md) | Start and stop a recording and read the automation | 5 min |
| 6 | [Flows as tools](06-flows-as-tools.md) | Register, run, stop and remove flows, and register a capability | 8 min |
| 7 | [Resolve a locator](07-resolve-a-locator.md) | Ask an OpenAI model for an element locator | 4 min |
| 8 | [Refresh the catalog](08-refresh-the-catalog.md) | Call `mcp/sync` after a plugin change | 2 min |

Stuck? See [Troubleshooting](troubleshooting.md).

Back to the [Hub documentation](../README.md).
