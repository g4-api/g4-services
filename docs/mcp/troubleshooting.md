# Troubleshooting

[⬅ Back to overview](README.md)

Quick fixes for the problems you are most likely to hit while using G4 from an AI agent. Message texts below are copied from the Hub source.

| Symptom | Cause | Fix |
| --- | --- | --- |
| The client cannot connect, or reports an unsupported transport | The endpoint speaks Streamable HTTP only, over plain `POST` | Use an MCP client that supports Streamable HTTP, and give it `http://<host>:9944/api/v4/g4/mcp`. See [Module 2](02-register-a-client.md) |
| `406` on every request | The `Accept` header is missing or does not allow the answer types | Send `Accept: application/json, text/event-stream`. See [Module 1](01-endpoint-and-transport.md) |
| `415` on every request | The body is not sent as `application/json` | Send `Content-Type: application/json` |
| `400` on a request | The body is not a valid MCP protocol request | Compare it with the samples in `http\G4.Services.Mcp.http` |
| `tools/call` for a plugin name such as `OpenUrl` fails as an unknown tool | Plugins have no MCP entry of their own | Find the plugin with `g4.ResolveTools`, then run it with `g4.SendRule`. See [Module 3](03-discover-tools.md) and [Module 4](04-drive-a-session.md) |
| A new plugin or capability does not appear in discovery | The cached catalog has not been rebuilt yet | Call `GET /api/v4/g4/mcp/sync`, then ask again. See [Module 8](08-refresh-the-catalog.md) |
| The client never shows new tools after a change | A stateless server cannot send `tools/list_changed` | Run `tools/list` again, or reconnect the client |
| `g4.GetBuffer` fails with no readable message | The Hub raised `No buffer was found for sessionId '<id>'. The session may be invalid, expired, or not initialized.`, which the SDK hides from the client | Check the id against the `driverSession` from `g4.StartSession`, and read the Hub log. See [Module 4](04-drive-a-session.md) |
| `The rules argument is required and must be a non-empty array.` | `g4.SendRules` got no `rules`, or an empty array | Send at least one rule in `rules` |
| `The manifest argument is required to register a new capability.` | `g4.RegisterCapability` got no `manifest` | Send the full plugin manifest in `manifest`. See [Module 6](06-flows-as-tools.md) |
| `At least one enabled recorder is required.` | No entry in `settings.recorders` has `"enabled": true` | Enable a recorder, and make sure its service is running. See [Module 5](05-record-an-app.md) |
| `Stop the recording before requesting its automation.` | `g4.GetAutomation` was called while the recording was still running | Call `g4.StopRecording` first |
| `The recording session was not found.` | The recording `id` was never issued or was already discarded | Use the `id` that `g4.StartRecording` returned |
| `The flow '<namespace>.<key>' was not found. Please try re-adding the flow or reload the service.` | The flow is not stored under that namespace and key | List the flows with `g4.GetFlows`. The default namespace is `G4.System` |
| `g4.ResolveLocator` fails | A required OpenAI argument is missing, the address is wrong, or the server does not answer in the Chat Completions shape | Pass `openaiApiKey`, `openaiModel` and `openaiUri` in the call. The Hub setting `OPENAI_API_KEY` is not used here. See [Module 7](07-resolve-a-locator.md) |
| A flow run cannot be stopped | No `referenceId` was sent to `g4.InvokeFlow` | Send a unique `referenceId`, then pass it as `automationId` to `g4.StopFlowInvocation` |
| Port 9955 is already in use | The Hub Worker and the UIA recorder both default to 9955 | Change one of the two ports. See the [configuration reference](../configuration.md) |

Still stuck? Read the Hub log first. See [Logging](../install-and-run/06-logging.md).
