# Troubleshooting the G4 Hub

[⬅ Back to the Hub documentation](README.md)

This page lists the problems you are most likely to meet while you run and use the Hub. Every message below is copied from the source. Each card names the file and line where the message is built. Short fixes for install problems are also in [Install and run: troubleshooting](install-and-run/troubleshooting.md), and for MCP in [MCP: troubleshooting](mcp/troubleshooting.md).

## How the Hub reports an error

An exception that reaches the error middleware becomes a problem-details answer. The status comes from the root cause of the exception.

| Root cause | Status | Title |
| --- | --- | --- |
| `InvalidCredentialException` | 401 | Invalid credentials provided. |
| `NoAvailableRunningTimeException` | 403 | No available running time. |
| `ArgumentException`, `JsonException` | 400 | Invalid request. |
| `KeyNotFoundException` | 404 | Resource not found. |
| `InvalidOperationException` | 409 | The request conflicts with the current state. |
| `HttpRequestException` | 502 | An upstream service failed. |
| Anything else | 500 | An unexpected error occurred. |

The `detail` field holds the message of the root cause. Read it first. Source: `src\G4.Services.Domain.V4\Middlewares\ErrorHandlingMiddleware.cs:176-184`.

A missing resource is not an exception on most routes. The controller answers `404` with a body that names the problem, such as `FlowNotFound`. Read the `errors` entry, not only the status.

## Start and connect

### Port 9955 is already in use

Cause: the Hub Worker and the UIA recorder both default to port 9955.

Check: look at the `Kestrel:Endpoints:Http:Url` value of the Worker and the recorder.

Fix: change one of the two ports. See the port table in [Configuration](configuration.md).

Source: `src\G4.Services.Worker\appsettings.json:5`, and `g4-recorders\src\G4.Recorders.Uia\appsettings.json`.

### A browser tool shows a CORS error

Cause: the page origin is not in `AllowedOrigins`. When `ORIGINS` is set, it replaces the file value.

Check: compare the origin in the browser message, with scheme and port, to the configured list.

Fix: add the exact origin to `ORIGINS`, separated by semicolons, and restart the Hub. See [Ports and CORS](install-and-run/02-ports-and-cors.md).

Source: `src\G4.Services.Hub\Program.cs:102-107`.

### Flows, environments or templates are gone after a restart

Cause: in Docker the data lived inside the container, and the container was removed. Or the encryption key changed.

Check: run `docker inspect g4-hub` and look for a mount on `/app/data`.

Fix: mount a volume on `/app/data` and keep the same `G4__StateManager__DataEncryptionKey`. See [Data folder and volume](install-and-run/03-data-folder-and-volume.md).

Source: `g4-engine\src\G4.Cache\CacheManager.cs:1221-1236`, and `docker\G4.Services.Hub.Dockerfile`.

### An MCP client cannot connect

Cause: the MCP endpoint speaks Streamable HTTP over plain `POST` only. It does not offer a stream or a session.

Check: send the `initialize` request from `http\G4.Services.Mcp.http` with `curl`.

Fix: use a client that supports Streamable HTTP and give it `http://localhost:9944/api/v4/g4/mcp`. See [MCP: endpoint and transport](mcp/01-endpoint-and-transport.md).

Source: `src\G4.Services.Domain.V4\Extensions\McpServiceExtensions.cs:33-45`.

## Run an automation

### `POST /automation/invoke` answers `200` with an empty body `{}`

Cause: the engine could not run the automation, for example because the grid or the driver failed to start. The client logs the error and returns the responses it collected, which is none. The Hub passes that result on as `200`.

Check: read the Hub log for `An error occurred while invoking the automation process.`.

Fix: fix the driver or the grid named in the log. Check `driverBinaries` first. See [Scale out](scale-out.md).

Source: `g4-api-client\src\G4.Api\Clients\AutomationClient.cs:322-325`, and `src\G4.Services.Hub.Api.V4\Controllers\AutomationController.cs:60`.

### `401 Invalid credentials provided.` or `403 No available running time.`

Cause: the engine rejected the token or the license. The Hub itself has no authentication middleware and does not check tokens.

Check: send a token in `authentication.token` of the request body. See [Tokens](install-and-run/04-tokens.md).

Fix: use a valid token, or renew the license so that running time is available.

Source: `src\G4.Services.Domain.V4\Middlewares\ErrorHandlingMiddleware.cs:176-179`.

### `InvalidDataSource`

Cause: the `dataSource` of the automation cannot be read. The source is not a file path, a JSON array or the Base64 of a JSON array, or its rows or filter are not valid. The answer is `400`.

Check: read the message in the `errors` entry. It says which source failed and why.

Fix: correct the `dataSource` and send the request again.

Source: `src\G4.Services.Hub.Api.V4\Controllers\AutomationController.cs:205`.

### `The plugin name '<name>' is provided by more than one namespace: <candidates>. Use the qualified name (Namespace:Key) of the plugin to run.`

Cause: two plugin packs ship a plugin with the same bare name, so the engine cannot choose.

Check: the message lists the candidates, such as `G4.Plugins.Common:SetCondition`.

Fix: write the rule with the qualified `Namespace:Key` name. See [Plugin references](../../g4-engine/docs/understand-a-g4-automation/07-plugin-references.md).

Source: `g4-engine\src\G4.Plugins\PluginFactory.cs:208-209`.

### `The plugin with the name '<name>' was not found. Ensure the plugin name is correct and that it is properly registered.`

Cause: no loaded plugin has that name, or the plugin pack was not loaded.

Check: look the plugin up with `GET /api/v4/g4/integration/manifests/key/{key}` or with the MCP tool `g4.FindTool`.

Fix: correct the name, or add the plugin pack and sync the cache.

Source: `g4-engine\src\G4.Plugins\PluginFactory.cs:212-213`.

## Flows, templates and environments

### `The flow '<namespace>.<key>' was not found. Please try re-adding the flow or reload the service.`

Cause: no flow is stored under that namespace and key. The Hub answers `404` with a body, not an exception. The default namespace is `G4.System`.

Check: list the flows with `GET /api/v4/g4/flows`.

Fix: register the flow with `PUT /api/v4/g4/flows`, or correct the namespace and key.

Source: `src\G4.Services.Domain.V4\Repositories\FlowsRepository.cs:90-91`, and `src\G4.Services.Hub.Api.V4\Controllers\FlowsController.cs:255`.

### `The flow manifest must have a key and an automation encoded as Base64 that deserializes into a G4AutomationModel.`

Cause: the flow was rejected. The key is missing, or the `automation` field is not Base64 of a valid automation. The answer is `400`.

Check: decode the field with `base64 -d` and parse the JSON.

Fix: encode the automation again. Use `base64 -w0` on Linux, so the value has no line breaks.

Source: `src\G4.Services.Domain.V4\Repositories\FlowsRepository.cs:53-55`.

### `The flow could not be decoded into an automation, or it is not valid JSON after its parameters were applied. ...`

Cause: a parameter value holds quotes or backslashes. Values are inserted as they are, and they break the JSON. The answer is `400`.

Check: look at the values you send in `parameters`.

Fix: escape quotes and backslashes in the value, or pass the value through a different route.

Source: `src\G4.Services.Domain.V4\Repositories\FlowsRepository.cs:130-133`.

### `The flow data source cannot be read: <reason>`

Cause: the data source of the flow cannot be read. The answer is `400`.

Check: the reason names the source that failed.

Fix: correct the data source in the flow, then register the flow again.

Source: `src\G4.Services.Domain.V4\Repositories\FlowsRepository.cs:138-139`.

### `InvalidManifest` when you add a template

Cause: a parameter or property name of the template cannot be offered as a tool, so the Hub refuses it with `400`. A `409` with the same name means the cache found a conflict, for example a duplicate identity.

Check: read the message in the `errors` entry.

Fix: rename the parameter, or change the identity of the template.

Source: `src\G4.Services.Hub.Api.V4\Controllers\TemplatesController.cs:50` and `:70`.

### `The template with key '<key>' was not found in the cache. Please try re-adding the template or reload the service.`

Cause: the template is not in the cache. This happens after data loss, or when it was never added. The answer is `404`.

Check: list the templates with `GET /api/v4/g4/templates`.

Fix: add the template again with `PUT /api/v4/g4/templates`.

Source: `src\G4.Services.Hub.Api.V4\Controllers\TemplatesController.cs:115`.

### `The environment with the name '<name>' was not found.` and `Parameter not found in environment '<environment>'.`

Cause: the environment name or the parameter name does not exist. Names that differ only by case are treated as the same environment.

Check: read the environment with `GET /api/v4/g4/environments/{name}?decode=true`.

Fix: upload the environment with `PUT /api/v4/g4/environments/{name}`, or correct the parameter name.

Source: `src\G4.Services.Hub.Api.V4\Controllers\EnvironmentsController.cs:78`, `:105` and `:158`.

### `The credential with the ID or name '<idOrName>' was not found.`

Cause: no credential has that id or name. The answer is `404`.

Check: list credentials with `GET /api/v4/g4/credentials`.

Fix: create the credential, or use the right id or name.

Source: `src\G4.Services.Hub.Api.V4\Controllers\CredentialsController.cs:61`.

### `Bot with ID '<id>' not found.` and `Cannot unregister bot while its SignalR connection is active; disconnect first.`

Cause: the bot id is unknown, or you tried to unregister a bot that is still connected. The answers are `404` and `409`.

Check: list the bots with `GET /api/v4/g4/bots/status`.

Fix: disconnect the bot, then unregister it.

Source: `src\G4.Services.Hub.Api.V4\Controllers\BotsController.cs:110` and `:182`.

## Recordings

### `At least one enabled recorder is required.`

Cause: no entry of `recorders` has `"enabled": true`.

Check: look at the start body.

Fix: enable a recorder and make sure its service is running. See [Recording Automations](recordings-api.md).

Source: `src\G4.Services.Domain.V4\Repositories\RecordingsRepository.cs:346`.

### `Authentication is required.`

Cause: the start body has no `authentication` object.

Fix: send `"authentication": { "token": "<your-g4-token>" }`.

Source: `src\G4.Services.Domain.V4\Repositories\RecordingsRepository.cs:386`.

### `Recorder endpoint must be an absolute HTTP(S) origin.`

Cause: `baseUrl` is missing, relative or not `http` or `https`.

Fix: use a value such as `http://localhost:9956`.

Source: `src\G4.Services.Domain.V4\Repositories\RecordingsRepository.cs:402`.

### `Recorder driver must be ChromeDriver or UiaDriver.` and `Recorder mode must be standard, user32, or coordinate.`

Cause: the Hub records only through the Chrome and UIA recorders. A SAP recorder is refused here. Chromium always records in `standard` mode.

Fix: set `driverParameters.driver` to `ChromeDriver` or `UiaDriver`, and use a supported mode.

Source: `src\G4.Services.Domain.V4\Repositories\RecordingsRepository.cs:415` and `:426`.

### `Pre/post scripts are deferred to a later layer; disable their flags.`

Cause: a recorder has `preScript` or `postScript` enabled. The Hub does not run them.

Fix: set both `enabled` flags to `false`.

Source: `src\G4.Services.Domain.V4\Repositories\RecordingsRepository.cs:435`.

### `Enabled recorder endpoints must be unique.` and `A requested recorder endpoint is already recording.`

Cause: two entries use the same endpoint, or another session already owns it. The first is `400`, the second `409`.

Fix: use one entry for each endpoint, and stop the other session first.

Source: `src\G4.Services.Domain.V4\Repositories\RecordingsRepository.cs:363` and `:147`.

### `No recorder started. <url>: <error>`

Cause: every recorder failed to start. The answer is `502`. A partial start returns the session with an error for each recorder.

Check: the message lists each endpoint and its error. Open the recorder ping route. See [Install and Operate the Recorders](../../g4-recorders/docs/install-and-operate/README.md).

Fix: start the recorder service and use the right port.

Source: `src\G4.Services.Domain.V4\Repositories\RecordingsRepository.cs:194-195`.

### `Stop the recording before requesting its automation.`

Cause: you asked for the flow while the recording was still running. The answer is `409`.

Fix: call `POST /api/v4/g4/recordings/{id}/stop` first.

Source: `src\G4.Services.Domain.V4\Repositories\RecordingsRepository.cs:89`.

### `The recording session was not found.`

Cause: the id was never issued, or the Hub restarted. Recording sessions live in memory only. The answer is `404`.

Fix: start a new session and use the id from the start answer.

Source: `src\G4.Services.Domain.V4\Repositories\RecordingsRepository.cs:321`.

### `The recording session is <state>; only a recording session can be paused.`

Cause: you suspended or resumed a session that is not recording. The answer is `409`.

Fix: check the state with `GET /api/v4/g4/recordings/{id}`.

Source: `src\G4.Services.Domain.V4\Repositories\RecordingsRepository.cs:473`.

### `The recorder '<baseUrl>' is not part of this recording session.`

Cause: the `baseUrl` in a suspend or resume body does not match any recorder of the session. The answer is `400`.

Fix: send the same URL you used at start.

Source: `src\G4.Services.Domain.V4\Repositories\RecordingsRepository.cs:484`.

## Workers and MCP tools

### `202` from `async/start`, but nothing runs

Cause: no Worker is connected, or the Worker points to the wrong Hub.

Fix: check `HUB_URI` on the Worker. In compose it is `http://hub:9944`. See [Scale out](scale-out.md).

Source: `src\G4.Services.Domain.V4\G4HubListener.cs:151-185`.

### `No buffer was found for sessionId '<id>'. The session may be invalid, expired, or not initialized.`

Cause: the session id is wrong or the session was removed. The MCP SDK hides this message from the client.

Fix: use the `driverSession` that `g4.StartSession` returned, and read the Hub log.

Source: `src\G4.Services.Domain.V4\Repositories\ToolsRepository.cs:876`.

### `The rules argument is required and must be a non-empty array.` and `The manifest argument is required to register a new capability.`

Cause: `g4.SendRules` got no rules, or `g4.RegisterCapability` got no manifest.

Fix: send the missing argument.

Source: `src\G4.Services.Domain.V4\Repositories\ToolsRepository.cs:1247` and `:1111`.

### `g4.ResolveLocator` fails

Cause: the tool reads the OpenAI key, model and address from its own arguments. It does not read `OPENAI_API_KEY` or the Hub OpenAI settings.

Fix: pass `openaiApiKey`, `openaiModel` and `openaiUri` in the call. See [Resolve a locator](mcp/07-resolve-a-locator.md).

Source: `src\G4.Services.Domain.V4\Repositories\ToolsRepository.cs:974-1048`.

## Still stuck?

Read the Hub log first. See [Logging](install-and-run/06-logging.md).
