# Module 1: Endpoint and transport

[⬅ Back to overview](README.md)

Where you are: the start of the path. About 5 minutes.

Before you register anything, know exactly what the Hub offers. The MCP endpoint is small and strict, and most connection problems come from missing one of these facts.

## What you'll do

- Learn the endpoint address and the transport
- Know what the endpoint does not support
- Count the tools and see where they come from

## Steps

1. Note the address. The endpoint is one route on the Hub port:

   ```text
   http://localhost:9944/api/v4/g4/mcp
   ```

   The route is the constant `McpRoute`. The port comes from your Hub settings. See the [configuration reference](../configuration.md).

2. Know the transport. It is Streamable HTTP in stateless mode. Every `POST` is complete on its own. The Hub issues no MCP session ID, so you send no session header. The server reports the name `g4-engine-mcp` and the version `4.0.0` when a client initializes.

3. Send the right headers. The sample requests in the Hub repository use these three:

   ```text
   Content-Type: application/json
   Accept: application/json, text/event-stream
   MCP-Protocol-Version: 2025-11-25
   ```

   A request with a missing or wrong `Accept` header gets `406`. A body that is not `application/json` gets `415`. A bad protocol request gets `400`. A notification, such as `notifications/initialized`, gets `202` and no body.

4. Know the limits. The endpoint accepts `POST` only. The Swagger description says that `GET` streaming and `DELETE` session closing are unavailable. Because the server is stateless, it cannot send `tools/list_changed` notifications. Re-run `tools/list` to see catalog changes.

5. Know that there is no sign-in. The Hub has no authentication middleware, so anyone who can reach the port can call the endpoint. Protect the port with your network. See [Module 4 of the install path](../install-and-run/04-tokens.md).

6. Count the tools. The server registers three tool classes, and `tools/list` returns 22 tools:

   | Family | Count | Tools |
   | --- | --- | --- |
   | System | 12 | `g4.FindExamples`, `g4.FindTool`, `g4.FindTools`, `g4.GetBuffer`, `g4.RegisterCapability`, `g4.RemoveBuffer`, `g4.RemoveSession`, `g4.ResolveLocator`, `g4.ResolveTools`, `g4.SendRule`, `g4.SendRules`, `g4.StartSession` |
   | Recording | 4 | `g4.GetAutomation`, `g4.GetRecordingStatus`, `g4.StartRecording`, `g4.StopRecording` |
   | Flow | 6 | `g4.GetFlow`, `g4.GetFlows`, `g4.InvokeFlow`, `g4.RegisterFlow`, `g4.RemoveFlow`, `g4.StopFlowInvocation` |

7. Understand how plugins are reached. The G4 plugins (actions such as `OpenUrl`) have no tool entry of their own. A client finds them with `g4.ResolveTools`, `g4.FindTools` or `g4.FindTool`, then runs them with `g4.SendRule` or `g4.SendRules`. A `tools/call` with any other name gets the SDK's unknown-tool error.

8. Know how errors look. A bad argument, a bad JSON value, a missing key, an invalid operation or a failed HTTP call returns a normal result with `isError` set to `true`, and the message in the content, so the model can read it and correct the call. A missing flow is a normal not-found result, not a protocol error. Any other failure stays with the SDK, which logs it on the Hub and hides the details from the caller.

## Check your work

- You can write the full endpoint address for your Hub.
- You can name the three headers and say why there is no session header.
- You know that plugins are reached through the router tools, not by name.

## Where the facts come from

| Fact | Source |
| --- | --- |
| Route, server name, version, stateless mode | `src\G4.Services.Domain.V4\Extensions\McpServiceExtensions.cs:33-45, 60-68` |
| No list-changed notifications | `McpServiceExtensions.cs:57-60` |
| Headers and the `202`, `400`, `406`, `415` answers | `src\G4.Services.Domain.V4\Mcp\McpDocumentFilter.cs:70-78, 108-115`; `http\G4.Services.Mcp.http` |
| `POST` only | `McpDocumentFilter.cs:80` |
| Three tool classes | `src\G4.Services.Domain.V4\Mcp\McpToolTypes.cs:23-24` |
| Wire names of all tools | `src\G4.Services.Domain.V4\Mcp\G4ToolNames.cs` |
| Tool failures become `isError` results | `src\G4.Services.Domain.V4\Mcp\McpResults.cs:20-60` |
| Endpoint mapped without authentication | `src\G4.Services.Hub\Program.cs:220` |

Next: [Module 2: Register a client](02-register-a-client.md)
