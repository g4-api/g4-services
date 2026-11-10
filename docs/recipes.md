# Hub recipes

[⬅ Back to the Hub documentation](README.md)

Short, step-by-step jobs for the G4 Hub. Each recipe lists the steps, a check that proves it worked, and the pages that explain more. Every command and route comes from the Hub source or from another page in this folder. Examples use `<your-g4-token>` and `<driver-binaries-or-grid-url>` as placeholders. Replace them with your own values.

| # | Recipe |
| --- | --- |
| 1 | [Run the Hub in Docker with a data volume](#run-the-hub-in-docker-with-a-data-volume) |
| 2 | [Run the Hub, a Worker and a grid with compose](#run-the-hub-a-worker-and-a-grid-with-compose) |
| 3 | [Let Claude Code drive a browser](#let-claude-code-drive-a-browser) |
| 4 | [Let an agent store and call a flow](#let-an-agent-store-and-call-a-flow) |
| 5 | [Invoke a flow over REST with parameters](#invoke-a-flow-over-rest-with-parameters) |
| 6 | [Record an app through the API](#record-an-app-through-the-api) |
| 7 | [Run browsers on a Selenium grid](#run-browsers-on-a-selenium-grid) |
| 8 | [Start a Worker on another machine](#start-a-worker-on-another-machine) |
| 9 | [Watch a run live with SignalR](#watch-a-run-live-with-signalr) |
| 10 | [Use OpenAI for ResolveLocator](#use-openai-for-resolvelocator) |

## Recipes

### Run the Hub in Docker with a data volume

Steps:

1. Run the published image and mount a volume on `/app/data`. A named volume keeps the flows, templates and environments when the container is replaced.

   ```text
   docker run -d -p 9944:9944 -v g4-hub-data:/app/data --name g4-hub g4api/g4-hub:latest
   ```

2. Set a new encryption key before you store anything important. Add `-e G4__StateManager__DataEncryptionKey=<your-key>` to the command above, and keep the same value every time you start the container.

Verify: `curl http://localhost:9944/api/v4/g4/ping` answers `200`. Then run `docker rm -f g4-hub`, run the command again, and check that `curl http://localhost:9944/api/v4/g4/flows` still lists your flows.

Related: [Pick a mode](install-and-run/01-pick-a-mode.md), [Data folder and volume](install-and-run/03-data-folder-and-volume.md), [Configuration](configuration.md).

### Run the Hub, a Worker and a grid with compose

Steps:

1. Open a terminal in the `docker` folder of the repository.
2. Start the three services. The Worker has no published image, so compose builds it.

   ```text
   docker compose up --build
   ```

3. Queue an automation with `POST http://localhost:9944/api/v4/g4/automation/async/start`. Use `http://chrome:4444` as `driverBinaries`, because the Worker reaches the grid by its service name.

Verify: open `http://localhost:4444/status` and check that the grid is ready. After a few seconds `GET http://localhost:9944/api/v4/g4/automation/async/completed` lists the run.

If it fails: `202` from `start` with no result means no Worker is connected. Check that `HUB_URI` on the Worker is `http://hub:9944`.

Related: [Scale out with Workers and grids](scale-out.md).

### Let Claude Code drive a browser

Steps:

1. Register the Hub as an MCP server.

   ```text
   claude mcp add --transport http g4 http://localhost:9944/api/v4/g4/mcp
   ```

2. Ask Claude Code to find the tools with `g4.ResolveTools`, to start a session with `g4.StartSession`, and to run rules with `g4.SendRule`. The session call needs the driver, the driver binaries or grid address, and your token:

   ```json
   {
       "driver": "ChromeDriver",
       "driverBinaries": "<driver-binaries-or-grid-url>",
       "token": "<your-g4-token>"
   }
   ```

3. Keep the `driverSession` from the answer. Every later call needs it. When the work is done, ask Claude Code to call `g4.RemoveSession`.

Verify: the answer of `g4.StartSession` holds a `driverSession`, and a following `g4.SendRule` with the `OpenUrl` plugin opens the page. A call to `g4.GetBuffer` returns the rules the session ran.

The `claude mcp add` command was not run against a live Hub while this page was written. TODO(verify: live).

Related: [MCP: register a client](mcp/02-register-a-client.md), [Drive a session](mcp/04-drive-a-session.md).

### Let an agent store and call a flow

Steps:

1. Store a flow with `g4.RegisterFlow`. Declare the parameters callers may set.

   ```json
   {
       "key": "RecordedLogin",
       "summary": "Replays the recorded login.",
       "automation": { "stages": [] },
       "parameters": [
           { "name": "UserName", "default": "admin" }
       ]
   }
   ```

   Replace the empty `automation` with a real automation, for example the result of `g4.GetAutomation`.

2. Call it with `g4.InvokeFlow`. Set a `referenceId` if you may need to stop the run.

   ```json
   {
       "key": "RecordedLogin",
       "parameters": { "UserName": "root" },
       "referenceId": "recorded-login-1"
   }
   ```

3. To stop a run, call `g4.StopFlowInvocation` with the `referenceId` as `automationId`.

Verify: `g4.GetFlows` lists the flow, and `g4.InvokeFlow` returns the automation responses by group id. A missing flow gives a normal not-found result with a message, not a protocol error.

Related: [Flows as tools](mcp/06-flows-as-tools.md).

### Invoke a flow over REST with parameters

Steps:

1. Save an automation as `automation.json`. The page [Understand a G4 Automation](../../g4-engine/docs/understand-a-g4-automation/01-what-an-automation-is.md) has a complete one. Use `{{$ parameters.UserName }}` where a value should change.
2. Register the flow. The `automation` field is the Base64 text of the file. `base64 -w0` is the GNU form; on macOS use `base64 -i automation.json`.

   ```bash
   curl -X PUT "http://localhost:9944/api/v4/g4/flows" \
       -H "Content-Type: application/json" \
       -d "{\"key\": \"open-page\", \"namespace\": \"MyTeam\", \"automation\": \"$(base64 -w0 automation.json)\"}"
   ```

3. Invoke it with parameters and a reference id.

   ```bash
   curl -X POST "http://localhost:9944/api/v4/g4/flows/MyTeam/open-page/invoke" \
       -H "Content-Type: application/json" \
       -d '{"parameters": {"UserName": "admin"}, "referenceId": "my-run-1"}'
   ```

4. To stop the run while it works, call `DELETE /api/v4/g4/flows/invocations/my-run-1`.

Verify: the register call answers `204`, and the invoke call answers `200` with the responses. An unknown flow answers `404` with a `FlowNotFound` entry.

If it fails: a parameter value with quotes or backslashes can break the JSON. See [Troubleshooting](troubleshooting.md).

Related: [Flows API](rest-api/flows.md).

### Record an app through the API

Steps:

1. Start the recorder service you need. The usual ports are 9955 for UIA and 9956 for Chromium. See [Install and Operate the Recorders](../../g4-recorders/docs/install-and-operate/README.md).
2. Start a recording session. This example records a browser through the Chromium recorder on port 9956. For a Windows app, use the UIA recorder: `"baseUrl": "http://localhost:9955"` and `"driver": "UiaDriver"`.

   ```bash
   curl -X POST "http://localhost:9944/api/v4/g4/recordings" \
       -H "Content-Type: application/json" \
       -d '{"authentication": {"token": "<your-g4-token>"}, "recorders": [{"enabled": true, "baseUrl": "http://localhost:9956", "driverParameters": {"driver": "ChromeDriver"}}]}'
   ```

   Keep the session `id` from the answer.

3. Do the steps you want to record in the browser.
4. Stop the session and read the automation.

   ```bash
   curl -X POST "http://localhost:9944/api/v4/g4/recordings/<id>/stop"
   ```

Verify: the stop answer is a G4 automation. `GET /api/v4/g4/recordings/<id>/flow` returns the same automation again. An empty recording answers `204`.

Related: [Recording Automations](recordings-api.md), [Build a flow from events](../../g4-recorders/docs/events/build-a-flow.md).

### Run browsers on a Selenium grid

Steps:

1. Start a standalone Chrome grid. These are the settings the release workflow uses.

   ```text
   docker run --rm -d --name selenium-grid --shm-size=2g -p 4444:4444 -e SE_NODE_MAX_SESSIONS=5 -e SE_NODE_OVERRIDE_MAX_SESSIONS=true selenium/standalone-chrome:latest
   ```

2. Set `driverBinaries` to the grid address. A value that starts with `http://` or `https://` makes the driver remote.

   ```json
   {
       "driverParameters": {
           "driver": "ChromeDriver",
           "driverBinaries": "http://localhost:4444"
       }
   }
   ```

3. Run the automation as usual.

Verify: `http://localhost:4444/status` shows the grid as ready, and a session appears in the grid page while the run works. If the Hub runs in a container, use the address that the container can reach, not `localhost`.

If it fails: a run that answers `200` with `{}` usually means the grid or the driver failed to start. See [Troubleshooting](troubleshooting.md).

Related: [Scale out with Workers and grids](scale-out.md), [Driver table](../../g4-abstraction/docs/drivers.md).

### Start a Worker on another machine

Steps:

1. Get the Worker. No Worker image is published, so build it from the repository root.

   ```text
   docker build -f docker/G4.Services.Worker.Dockerfile -t g4-worker .
   ```

2. Run it with the address of the Hub.

   ```text
   docker run -d -p 9955:9955 -e HUB_URI=http://hub-host:9944 --name g4-worker g4-worker
   ```

   Without Docker, run `G4.Services.Worker run --hub http://hub-host:9944`.

3. Queue an automation with `POST /api/v4/g4/automation/async/start`.

Verify: the result of the queued automation appears at `GET /api/v4/g4/automation/async/completed`. One Worker runs one automation at a time, so start more Workers for more parallel runs.

If it fails: the port 9955 is also the default of the UIA recorder. Do not run both on one machine with default settings.

Related: [Scale out with Workers and grids](scale-out.md).

### Watch a run live with SignalR

Steps:

1. Connect to the notifications hub at `/hub/v4/g4/notifications`, and register handlers for the events you want before you start the connection.

   ```javascript
   const connection = new signalR.HubConnectionBuilder()
       .withUrl("http://localhost:9944/hub/v4/g4/notifications")
       .withAutomaticReconnect()
       .build();

   connection.on("ReceiveAutomationStartEvent", event => console.log("start", event.type, event.id));
   connection.on("ReceiveAutomationEndEvent", event => console.log("end", event.type, event.id));
   connection.on("StartAutomation", response => console.log("done", response));

   await connection.start();
   await connection.invoke("StartAutomation", automation);
   ```

2. Pass a `G4AutomationModel` as `automation`. The Hub sends events only to the connection that started the automation.

Verify: you see one `start` and one `end` line for each rule, then the final `StartAutomation` reply.

Related: [Monitor runs live over SignalR](signalr.md).

### Use OpenAI for ResolveLocator

Steps:

1. Start a session with `g4.StartSession` and open the page that holds the element.
2. Call `g4.ResolveLocator` and pass the OpenAI details in the call. The Hub settings `OPENAI_API_KEY` and `OPENAI_API_ENDPOINT` belong to a different feature and are not used here.

   ```json
   {
       "driverSession": "<driver-session-id>",
       "intent": {
           "agentIntent": "locate the login button",
           "userIntent": "click the login button"
       },
       "openaiApiKey": "<your-openai-api-key>",
       "openaiModel": "gpt-4.1-mini",
       "openaiUri": "<your-openai-chat-completions-url>",
       "token": "<your-g4-token>",
       "constraints": {
           "mustBeVisible": true
       }
   }
   ```

3. Use the returned locator in a rule sent with `g4.SendRule`.

Verify: the answer is a JSON locator that the model chose from the page. If a required argument is missing, the call fails before any request goes out.

The key travels in the tool arguments, so the agent and your MCP client logs can see it. Use a key with a limited budget.

Related: [Resolve a locator](mcp/07-resolve-a-locator.md).

## Where the facts come from

| Fact | Source |
| --- | --- |
| Image `g4api/g4-hub`, grid settings | `.github\workflows\release-pipline.yml` |
| Volume path `/app/data` | `docker\G4.Services.Hub.Dockerfile` |
| Compose services and `HUB_URI` | [Scale out](scale-out.md#run-everything-with-docker-compose) (compose text is on that page) |
| Flow, recording and async routes | `src\G4.Services.Hub.Api.V4\Controllers\` (see [REST API guide](rest-api/README.md)) |
| MCP tools and arguments | `src\G4.Services.Domain.V4\Mcp\` (see [MCP](mcp/README.md)) |
| `ResolveLocator` ignores the Hub OpenAI settings | `src\G4.Services.Domain.V4\Repositories\ToolsRepository.cs:974-1048` |
| Worker `run --hub` | `src\G4.Services.Worker\Cli\RunCommand.cs` |
