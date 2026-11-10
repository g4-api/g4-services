# G4 Hub Documentation

These pages explain how to install, configure and use the G4 Hub. The Hub is an ASP.NET Core service that serves the REST API, the MCP endpoint, the SignalR hubs and the Worker.

## Paths at a glance

| # | Page | Best for | What it covers | Status |
| --- | --- | --- | --- | --- |
| 1 | [Install and Run the G4 Hub](install-and-run/README.md) | Getting a Hub running | Release zip, Docker and source modes, ports, CORS, data folder, tokens, logging | available |
| 2 | [Configuration reference](configuration.md) | Tuning a Hub | One table per settings section, environment overrides and the port table | available |
| 3 | [Use G4 from an AI Agent](mcp/README.md) | Driving G4 from an AI agent | MCP endpoint, client registration, system, recording and flow tools | available |
| 4 | [REST API guide](rest-api/README.md) | Calling the Hub from code | One page per controller group with routes and examples | available |
| 5 | [Recording Automations](recordings-api.md) | Recording through the Hub | Recording session routes, status codes and request bodies | available |
| 6 | [Monitor runs over SignalR](signalr.md) | Watching runs live | Hub paths, methods, events and payloads with subscriber samples | available |
| 7 | [Scale out with Workers and grids](scale-out.md) | Running more than one worker | Sync and async invoke, Workers, grids and a compose file | available |
| 8 | [Troubleshooting](troubleshooting.md) | Fixing a problem | Symptom, cause and fix cards | available |
| 9 | [Recipes](recipes.md) | Common jobs | Short step-by-step recipes | available |

Back to the Hub README.
