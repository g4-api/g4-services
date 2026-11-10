# Monitor runs live over SignalR

[⬅ Back to the Hub documentation](README.md)

The Hub exposes three SignalR hubs. This page lists every method a client can call, every event the Hub sends back, the payload of each, and when it happens. It ends with a JavaScript and a C# subscriber.

## The three hubs

| Hub path | Class | Used by |
| --- | --- | --- |
| `/hub/v4/g4/notifications` | `G4AutomationNotificationsHub` | Clients that run an automation and want live progress. The embedded designer uses it (`wwwroot\js\global.js`, `BASE_NOTIFICATION_PATH`). |
| `/hub/v4/g4/orchestrator` | `G4Hub` | Workers. A Worker asks for queued automations and returns the result. |
| `/hub/v4/g4/bots` | `G4BotsHub` | Bots that register themselves. The bots page (`wwwroot\views\bots.html`) connects here as a viewer. |

Source: `src\G4.Services.Hub\Program.cs:259, 262, 265` (`MapHub`) and the hub classes in `src\G4.Services.Domain.V4\Hubs\`. The path prefix is `/hub/v{AppSettings.ApiVersion}/g4`, and the API version is 4. All three hubs use the Hub CORS policy, so browser clients must come from an allowed origin. See [Ports and CORS](install-and-run/02-ports-and-cors.md).

## How live events reach you

Progress events are not broadcast. The Hub sends each event only to one connection, the one named in the automation request. It reads the connection id from the environment variable `SignalR`, parameter `ConnectionId`. When that value is missing, the Hub sends nothing. Source: `src\G4.Services.Domain.V4\Extensions\LocalExtensions.cs` (`GetConnection`, `SendMessage`).

You get that value for free in one case. When you call `StartAutomation` on the notifications hub, the Hub writes your own connection id into `settings.environmentsSettings.environmentVariables.SignalR.parameters.ConnectionId` before it runs the automation. Source: `G4AutomationNotificationsHub.cs` (`StartAutomation`).

> **💡 Tip:** `StartAutomation` replaces the whole `environmentVariables` dictionary of your request with the `SignalR` entry. Environment variables you put there are not kept. Pass values through `parameters` instead.

## Notifications hub

Path: `/hub/v4/g4/notifications`.

### Methods you call

| Direction | Method | Payload type | When |
| --- | --- | --- | --- |
| Client to Hub | `StartAutomation` | `G4AutomationModel` | You want to run an automation and watch it. The call runs the automation to the end and then answers on `StartAutomation`. |
| Client to Hub | `InitializeAutomation` | `G4AutomationModel` | You want the Hub to validate and complete the request without running it. The Hub answers on `InitializeAutomation`. |
| Client to Hub | `SendHeartbeat` | none | You want to check that the connection is alive. The Hub answers on `SendHeartbeat`. |

### Events you receive

| Direction | Event | Payload type | When |
| --- | --- | --- | --- |
| Hub to client | `StartAutomation` | `G4AutomationResponseModel`, or `AutomationErrorModel` on failure or stop | The automation finished, failed, or was stopped. |
| Hub to client | `InitializeAutomation` | The initialized automation response | After you call `InitializeAutomation`. |
| Hub to client | `SendHeartbeat` | `string` | After you call `SendHeartbeat`. The text is `Heartbeat received, connection is active.` |
| Hub to client | `ReceiveAutomationRequestInitializedEvent` | `EventDataModel` | The request was initialized. `type` is `Automation`. |
| Hub to client | `ReceiveAutomationStartEvent` | `EventDataModel` | An automation, stage, job or rule is about to run. |
| Hub to client | `ReceiveAutomationEndEvent` | `EventDataModel` | A stage, job or rule finished. |
| Hub to client | `ReceiveAutomationInvokedEvent` | `EventDataModel` | The whole automation finished. `value` holds `request` and `response`. |
| Hub to client | `ReceiveLogCreatingEvent` | `EventDataModel` | A log entry is about to be written. `type` is `Log`. |
| Hub to client | `ReceiveLogCreatedEvent` | `EventDataModel` | A log entry was written. `type` is `Log`. |

Source: `G4AutomationNotificationsHub.cs` for the methods and the three replies. `src\G4.Services.Domain.V4\IDomain.cs` (`InitializeSyncClient`) for the `Receive*` events.

How the start and end events map to the engine events:

| Event | Sent when the engine raises | `objectType` | `type` | `value` |
| --- | --- | --- | --- | --- |
| `ReceiveAutomationStartEvent` | `AutomationInvoking` | `G4AutomationModel` | `Automation` | The automation. |
| `ReceiveAutomationStartEvent` | `StageInvoking` | `G4StageModel` | `Stage` | The stage. |
| `ReceiveAutomationStartEvent` | `JobInvoking` | `G4JobModel` | `Job` | The job. |
| `ReceiveAutomationStartEvent` | `RuleInvoking` | The rule class name | The plugin type from the manifest, or `SystemPlugin` | The rule. |
| `ReceiveAutomationEndEvent` | `StageInvoked` | `G4StageModel` | `Stage` | The stage. |
| `ReceiveAutomationEndEvent` | `JobInvoked` | `G4JobModel` | `Job` | The job. |
| `ReceiveAutomationEndEvent` | `RuleInvoked` | The rule class name | The plugin type from the manifest, or `SystemPlugin` | An object with `rule`, `extractions` and `exceptions`. |

Source: `IDomain.cs` (`InitializeSyncClient`).

### The event envelope

Every `Receive*` event carries one `EventDataModel` argument.

| Field | Meaning |
| --- | --- |
| `id` | The reference id of the entity. For log events, the invoker name. |
| `objectType` | The class name of the entity. |
| `type` | The kind of entity, such as `Automation`, `Stage`, `Job`, `Log`, or a plugin type. |
| `value` | The full state of the entity when the event fired. |

Source: `IDomain.cs` (nested `EventDataModel`). The field names above are camelCase because that is how the bots page reads its payloads (`bot.id` in `bots.html`). `TODO(verify)`: the casing of `EventDataModel` on the wire was not checked against a live connection.

### The error reply

When the automation fails or is stopped, `StartAutomation` carries an `AutomationErrorModel`, not an exception.

```json
{
    "errors": {
        "Automation": [
            "Automation stopped"
        ]
    },
    "traceId": "20261108-101500-1234567"
}
```

| Field | Meaning |
| --- | --- |
| `errors` | Messages keyed by the failing area. The key is `Automation`. |
| `request` | The automation that failed. Missing when the reply reports a stop. |
| `traceId` | A time-based id in the form `yyyyMMdd-HHmmss-fffffff`. |

Source: `src\G4.Services.Domain.V4\Models\AutomationErrorModel.cs`, `G4AutomationNotificationsHub.cs`.

## Orchestrator hub

Path: `/hub/v4/g4/orchestrator`. A Worker uses it to take queued automations. See [Scale out with Workers and grids](scale-out.md).

| Direction | Method or event | Payload type | When |
| --- | --- | --- | --- |
| Client to Hub | `SendHeartbeat` | none | The Worker sends it every 10 seconds. |
| Hub to client | `ReceiveHeartbeat` | `string` | Reply to `SendHeartbeat`. |
| Client to Hub | `GetPendingAutomation` | none | The Worker polls once a second while it is idle. |
| Hub to client | `ReceivePendingAutomation` | `G4QueueModel`, or null when the queue is empty | Reply to `GetPendingAutomation`. |
| Client to Hub | `CompleteAutomation` | `key` (`string`) and `response` (`G4AutomationResponseModel`) | The Worker finished the automation. |
| Hub to client | `ConfirmAutomationResponse` | `string` (the key) | Reply to `CompleteAutomation`. |

Source: `G4Hub.cs`, and `G4HubListener.cs` for the intervals (`StartG4HubListener`, `StartHeartbeatListener`).

When the queued automation carries a `SignalR.ConnectionId`, the engine raises `ReceiveAutomationInvokedEvent` on this hub. It sends the event to that connection. Source: `IDomain.cs` (`InitializeAsyncClient`).

## Bots hub

Path: `/hub/v4/g4/bots`.

| Direction | Method or event | Payload type | When |
| --- | --- | --- | --- |
| Client to Hub | `SendHeartbeat` | none | You want to check the connection. |
| Hub to client | `ReceiveHeartbeat` | `string` | Reply to `SendHeartbeat`. |
| Client to Hub | `RegisterBot` | `ConnectedBotModel` | A bot announces itself after it connects. The Hub stores it with status `Ready`. |
| Hub to client | `ReceiveRegisterBot` | An object with `StatusCode` (200) and `Message` | Reply to `RegisterBot`. |
| Client to Hub | `UpdateBot` | `ConnectedBotModel` | A bot reports a new status. Ignored when the bot is not registered. |
| Hub to client | `ReceiveBotUpdated` | `ConnectedBotModel` | Reply to `UpdateBot`. |

`ConnectedBotModel` fields: `id`, `connectionId`, `name`, `type`, `machine`, `osVersion`, `status`, `createdOn`, `lastModifiedOn`, `callbackHost`, `callbackIngress`, `callbackPort`, `callbackUri`. Source: `src\G4.Services.Domain.V4\Models\ConnectedBotModel.cs`.

Rules from `G4BotsHub.cs`:

- A bot's id is `<id>-<connectionId>`, or the connection id when you send no id.
- When a connection closes, the Hub removes every bot whose id contains that connection id.
- Replies go only to the caller. A viewer such as the bots page receives no updates from other bots. The comment in `bots.html` says the same.

## Connection tuning

The Hub builds its SignalR options from `AppSettings.HubOptions`. Only four values are applied; the rest of `HubOptionsSettings` is declared but not copied into the SignalR options.

| Setting | Default | Applied by `AddSignalR` | Effect |
| --- | --- | --- | --- |
| `EnableDetailedErrors` | `true` | Yes | Sends exception details to clients. |
| `MaximumReceiveMessageSize` | `long.MaxValue` | Yes | Largest message the Hub accepts. |
| `KeepAliveInterval` | 15 seconds | Yes | How often the Hub pings clients. |
| `ClientTimeoutInterval` | 60 seconds | Yes | How long the Hub waits before it treats a silent client as gone. |
| `HandshakeTimeout`, `StreamBufferCapacity`, `StatefulReconnectBufferSize`, `SupportedProtocols`, `DisableImplicitFromServicesParameters` | See the model | No | Declared in `HubOptionsSettings` only. |

Source: `src\G4.Settings\Models\HubOptionsSettings.cs`, `src\G4.Services.Domain.V4\Extensions\PublicExtensions.cs` (`HubOptions.New`), `src\G4.Services.Hub\Program.cs:124`.

## A JavaScript subscriber

The Hub serves the SignalR client as a local file, so no CDN is needed. Load it the way the Hub pages do, then connect.

```html
<script src="/js/signalr.min.js"></script>
```

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:9944/hub/v4/g4/notifications")
    .withAutomaticReconnect()
    .build();

connection.on("ReceiveAutomationStartEvent", event => {
    console.log("start", event.type, event.id);
});

connection.on("ReceiveAutomationEndEvent", event => {
    console.log("end", event.type, event.id);
});

connection.on("StartAutomation", response => {
    console.log("done", response);
});

await connection.start();

// automation is a G4AutomationModel object.
await connection.invoke("StartAutomation", automation);
```

The `withUrl` and `withAutomaticReconnect` calls follow `wwwroot\js\global.js`. When the page is served by the Hub itself, use the relative path `/hub/v4/g4/notifications`.

## A C# subscriber

Use the `Microsoft.AspNetCore.SignalR.Client` package. The Worker builds its connection the same way (`G4HubListener.cs`).

```csharp
using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:9944/hub/v4/g4/notifications")
    .WithAutomaticReconnect()
    .Build();

connection.On<System.Text.Json.JsonElement>("ReceiveAutomationStartEvent", e =>
    Console.WriteLine($"start {e.GetProperty("type")} {e.GetProperty("id")}"));

connection.On<System.Text.Json.JsonElement>("ReceiveAutomationEndEvent", e =>
    Console.WriteLine($"end {e.GetProperty("type")} {e.GetProperty("id")}"));

var completed = new TaskCompletionSource<System.Text.Json.JsonElement>();
connection.On<System.Text.Json.JsonElement>("StartAutomation", completed.SetResult);

await connection.StartAsync();

// automation is a G4AutomationModel object.
await connection.InvokeAsync("StartAutomation", automation);

var response = await completed.Task;
```

> **💡 Tip:** `InvokeAsync("StartAutomation", ...)` returns when the Hub method returns, which is after the final `StartAutomation` reply was sent. Register the handler before you start the connection, as above.

## Verify

1. Start the Hub. See [Install and run](install-and-run/README.md).
2. Run the JavaScript subscriber in a page served by the Hub, or run the C# one.
3. Send a small automation, for example the one in the Hub README.
4. Check that you see one `start` and one `end` line per rule, then the final `StartAutomation` reply.

## If it fails

| Symptom | Cause | Fix |
| --- | --- | --- |
| The browser blocks the connection | The page origin is not allowed. | Add the origin to `AllowedOrigins` or `ORIGINS`. See [Ports and CORS](install-and-run/02-ports-and-cors.md). |
| You get the final reply but no start or end events | The request had no `SignalR.ConnectionId`, which happens when you call the REST invoke endpoint, not `StartAutomation`. | Call `StartAutomation` on the notifications hub, or pass the connection id yourself. |
| The final reply is an error with `Automation stopped` | Someone stopped the automation, for example with `automation/stop/{automationId}`. | Start it again. |
| The connection drops after 60 seconds of silence | The client sends no traffic and no keep-alive. | Use a SignalR client that sends keep-alive pings, or call `SendHeartbeat`. |
