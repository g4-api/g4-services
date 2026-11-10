# Module 6: Flows as tools

[⬅ Back to overview](README.md) · [⬅ Module 5](05-record-an-app.md)

Where you are: you have an automation from a recording. About 8 minutes.

A flow is an automation stored on the Hub under a key. Flows are shared with REST, so a flow stored here can be run from the designer or from a script, and the other way round. The six flow tools store, list, read, run, stop and remove flows. `g4.RegisterCapability` is a different tool, and it comes at the end.

## What you'll do

- Store an automation as a flow with parameters
- List and read flows
- Run a flow and stop a run
- Remove a flow
- Register a capability

## Steps

1. Know the two identity fields. Every flow tool that names a flow takes a `key` and an optional `namespace`. Flow keys are unique within a namespace. When you leave the namespace out, the Hub uses `G4.System`.

2. Store a flow with `g4.RegisterFlow`.

   Purpose, quoted: "Registers a native G4 automation as a stored flow, or overwrites the flow with the same namespace and key. Pass the automation as a JSON object, for example the result of g4.GetAutomation; the tool encodes it as Base64. Declare parameters to expose {{$ parameters.Name }} placeholders to g4.InvokeFlow callers."

   | Argument | Type | Required | Meaning |
   | --- | --- | --- | --- |
   | `key` | string | Yes | The flow key |
   | `automation` | object | Yes | The native G4 automation JSON object to store |
   | `namespace` | string | No | The flow namespace |
   | `aliases` | array of strings | No | Alternative keys that also identify the flow |
   | `summary` | string | No | A one-line summary shown when the flow is discovered |
   | `description` | string | No | A longer description of what the flow does |
   | `categories` | array of strings | No | Categories used to group the flow |
   | `parameters` | array of objects | No | Parameters callers can supply to `g4.InvokeFlow` |

   Each entry in `parameters` has a required `name`, and optional `default`, `description` and `mandatory`:

   ```json
   {
       "jsonrpc": "2.0",
       "id": 40,
       "method": "tools/call",
       "params": {
           "name": "g4.RegisterFlow",
           "arguments": {
               "key": "RecordedLogin",
               "summary": "Replays the recorded login.",
               "automation": { "stages": [] },
               "parameters": [
                   { "name": "UserName", "default": "admin" }
               ]
           }
       }
   }
   ```

   The `"stages": []` value only shows the position of the automation. Use the real result of `g4.GetAutomation` in your own call. The answer repeats the `namespace` and the `key`.

3. List and read flows.

   | Tool | Purpose, quoted | Arguments |
   | --- | --- | --- |
   | `g4.GetFlows` | "Lists the stored flows with their manifests. Filter by namespace to narrow the result." | `namespace` (optional). The answer has `count` and `flows` |
   | `g4.GetFlow` | "Gets the stored manifest of one flow, including its parameters and its Base64 automation." | `key` (required), `namespace` (optional) |

   A flow that does not exist is a normal not-found result, with a message in the content. It is not a protocol error.

4. Run a flow with `g4.InvokeFlow`.

   Purpose, quoted: "Runs a stored flow and waits for it to finish. Parameter values replace the {{$ parameters.Name }} placeholders anywhere in the flow; send them as parameters, as an argument in command-line form ({{$ --Name:value}}), or both, and on a name in both the argument value is used. A declared parameter without a value receives its default. The caller's authentication token always replaces the flow's token; the other authentication, driverParameters, and settings values only fill what the flow leaves out. Set referenceId to a unique value to stop the run with g4.StopFlowInvocation. Returns the automation responses by group ID."

   | Argument | Type | Required | Meaning |
   | --- | --- | --- | --- |
   | `key` | string | Yes | The flow key or alias |
   | `namespace` | string | No | The flow namespace |
   | `parameters` | object | No | Parameter values by name |
   | `argument` | string | No | Parameter values in command-line form, for example `{{$ --Retries:3}}` |
   | `referenceId` | string | No | A unique automation ID for this run, used by `g4.StopFlowInvocation` |
   | `authentication` | object | No | The caller's G4 authentication, for example the token |
   | `driverParameters` | object | No | Driver parameters such as `driver` and `driverBinaries`, filling what the flow leaves out |
   | `settings` | object | No | Automation settings that fill what the flow leaves out |

   ```json
   {
       "jsonrpc": "2.0",
       "id": 41,
       "method": "tools/call",
       "params": {
           "name": "g4.InvokeFlow",
           "arguments": {
               "key": "RecordedLogin",
               "parameters": { "UserName": "root" },
               "referenceId": "recorded-login-1"
           }
       }
   }
   ```

   The call blocks until the flow ends, the same as REST invoke.

5. Stop a run with `g4.StopFlowInvocation`.

   Purpose, quoted: "Stops a running flow invocation by its automation ID (the referenceId sent to g4.InvokeFlow, or the flow's own reference ID). Its driver sessions are closed, so the flow stops at its next step. Returns status stopped when the stop request completed, or failed with exitCode 1 on an error."

   It takes one required argument, `automationId`. Because `g4.InvokeFlow` blocks, a client usually calls this from a second request while the first one is still running.

6. Remove a flow with `g4.RemoveFlow`.

   Purpose, quoted: "Removes one stored flow from the Flows collection and the flows cache."

   It takes `key` (required) and `namespace` (optional). The answer holds `namespace`, `key` and `removed`.

7. Register a capability with `g4.RegisterCapability`.

   Purpose, quoted: "Registers a promoted G4 flow-template capability as a discoverable, callable tool. Accepts a full G4 plugin manifest (the same shape produced by the flow-to-template promotion pipeline) and persists it through the G4 engine's Templates store."

   | Argument | Type | Required | Meaning |
   | --- | --- | --- | --- |
   | `manifest` | object | Yes | The full G4 plugin manifest, the same shape that `PUT /api/v4/g4/templates` accepts |

   The Hub sets the manifest `source` to `Template` and answers `registered`, `key`, `source` and `pluginType`. A manifest that fails validation makes the call fail with the validation message. To find the new capability with the discovery tools, run the catalog sync in [Module 8](08-refresh-the-catalog.md). For the manifest fields, see the [plugin manifest reference](../../../g4-engine/docs/plugin-manifest-reference.md).

## Check your work

- `g4.RegisterFlow` answered with your key, and `g4.GetFlows` lists it.
- `g4.InvokeFlow` returned the automation responses.
- `g4.RemoveFlow` answered `removed` for your test flow.

TODO(verify: live): no Hub was running, so no answer was captured. The request samples are the ones in `http\G4.Services.Mcp.http` (requests 8 to 12).

## Where the facts come from

| Fact | Source |
| --- | --- |
| Tool wording | `src\G4.Services.Domain.V4\Mcp\FlowTools.cs`, nested `Catalog` class; `G4SystemTools.cs` for `g4.RegisterCapability` |
| Argument names and optional flags | `FlowTools.cs:53-220`; `src\G4.Services.UnitTests\Mcp\SystemToolsEndpointTests.cs:19-41` |
| Parameter entry fields | `src\G4.Services.Domain.V4\Mcp\FlowToolModels.cs:15-44` |
| Default namespace `G4.System` | `src\G4.Services.Domain.V4\Repositories\FlowsRepository.cs:27` |
| Not-found result, no exception | `FlowTools.cs:59-64, 194-199` |
| `RegisterCapability` source and answer | `src\G4.Services.Domain.V4\Repositories\ToolsRepository.cs:1104-1134` |

Next: [Module 7: Resolve a locator](07-resolve-a-locator.md)
