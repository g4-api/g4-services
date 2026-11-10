# G4 Hub Configuration Reference

[⬅ Back to the Hub documentation](README.md)

This page lists every setting in the Hub `appsettings.json` and the environment variables the Hub reads. Each table follows the same shape: `Key | Default | Env override | Read by | Effect`.

## How settings are loaded

- The Hub reads `appsettings.json` from its current working directory, not from the folder of the `.dll`. Start the Hub from the folder that holds the file. Source: `src\G4.Settings\AppSettings.cs:61-63, 100-115` (`SetBasePath(Directory.GetCurrentDirectory())`).
- Environment variables are added after the file, so they win. Nested keys use a double underscore, for example `G4__StateManager__BasePath`. This is the standard .NET configuration rule that `AddEnvironmentVariables()` applies. Source: `src\G4.Settings\AppSettings.cs:102, 115`.
- The defaults below are the values committed in `src\G4.Services.Hub\appsettings.json`. The Worker has its own file, `src\G4.Services.Worker\appsettings.json`.
- `src\G4.Services.Hub.Api.V4\appsettings.json` is a second copy. It uses `C:\\Temp\\data` for the data folder. The published Hub is built from `G4.Services.Hub`, so its file is the one that ships.

## Kestrel

| Key | Default | Env override | Read by | Effect |
| --- | --- | --- | --- | --- |
| `Kestrel:Endpoints:Http:Url` | `http://+:9944` | `Kestrel__Endpoints__Http__Url` | ASP.NET Core Kestrel (`Program.cs:35` calls `UseUrls()` with no arguments) | The address and port the Hub listens on. `+` means every network interface. |
| `AllowedHosts` | `*` | `AllowedHosts` | ASP.NET Core host filtering | Which `Host` header values the Hub accepts. |

The Worker uses the same key with `http://+:9955`. Source: `src\G4.Services.Worker\appsettings.json:5`.

## CORS

| Key | Default | Env override | Read by | Effect |
| --- | --- | --- | --- | --- |
| `AllowedOrigins` | `["http://localhost:8000", "http://localhost:5173"]` | `ORIGINS` | `src\G4.Services.Hub\Program.cs:102-107` | Browser origins that may call the Hub. |

Rules, from `Program.cs:102-120`:

- If `ORIGINS` is set and not empty, it replaces `AllowedOrigins`. Separate several origins with a semicolon, for example `http://localhost:8000;http://localhost:5173`.
- An origin is allowed when it matches an entry exactly, or when it starts with `vscode-webview://`. The second rule is always on.
- The policy allows any method, any header and credentials.

## G4:StateManager

| Key | Default | Env override | Read by | Effect |
| --- | --- | --- | --- | --- |
| `G4:StateManager:BasePath` | `data` | `G4__StateManager__BasePath` | `g4-engine\src\G4.Cache\CacheManager.cs:1216-1224` | Base folder for stored data. The engine adds a `data` subfolder, so the default becomes `data\data`. An empty value or `.` uses `<working directory>\data`. |
| `G4:StateManager:DataEncryptionKey` | `30908f87-8539-477a-86e7-a4c13d4583c4` | `G4__StateManager__DataEncryptionKey` | `CacheManager.cs:1227-1237` | Password of the encrypted `g4-engine.db` file. |

> **⚠️ Replace the committed key.** The default key is public. It is also the engine fallback when the value is empty (`CacheManager.cs:1219`). Set your own key before you store real data. Changing the key later makes the existing `g4-engine.db` unreadable, so choose it once.

The engine also opens a SQLite file for credentials. It is `Data\g4-data.db` under the base path. Note the capital `D`, which matters on Linux. Source: `CacheManager.cs:1251-1254`.

## G4:WorkerConfiguration

| Key | Default | Env override | Read by | Effect |
| --- | --- | --- | --- | --- |
| `G4:WorkerConfiguration:HubUri` | `http://localhost:9944` | `HUB_URI` (wins over the file) | `src\G4.Services.Domain.V4\G4HubListener.cs:151-185` (`ResolveHubUri`) | The Hub address a Worker connects to. The code falls back to `http://localhost:9944` when neither source is set. |
| `G4:WorkerConfiguration:HubApiVersion` | `4` | none | no reader found | Listed in the file. `TODO(verify)`: no code in `g4-services`, `g4-engine` or `g4-abstraction` reads it. |
| `G4:WorkerConfiguration:MaxParallel` | `5` | none | no reader found | Same note as above. Do not confuse it with the automation `settings.maxParallel`, which the engine does read. |
| `G4:WorkerConfiguration:ConnectionTimeout` | `600.0` | none | no reader found | Same note as above. |

The Worker command `run --hub <uri>` sets `HUB_URI` for its own process. Source: `src\G4.Services.Worker\Cli\RunCommand.cs:24-54`.

## G4:HubConfiguration and G4:PluginsConfiguration

| Key | Default | Env override | Read by | Effect |
| --- | --- | --- | --- | --- |
| `G4:HubConfiguration:CreationTimeout` | `30.0` | none | no reader found | `TODO(verify)`: present in all three `appsettings.json` files, but a search of `g4-services`, `g4-engine` and `g4-abstraction` finds no reader. |
| `G4:HubConfiguration:MaxCompleted` | `20` | none | no reader found | Same note. |
| `G4:HubConfiguration:RepairAttempts` | `3` | none | no reader found | Same note. |
| `G4:HubConfiguration:RunningTimeout` | `30.0` | none | no reader found | Same note. |
| `G4:PluginsConfiguration:Locations` | `[]` | none | no reader found | Same note. |

## G4:LoginManagerUri

| Key | Default | Env override | Read by | Effect |
| --- | --- | --- | --- | --- |
| `G4:LoginManagerUri` | `https://g4api.azurewebsites.net` | `G4__LoginManagerUri` | `src\G4.Settings\AppSettings.cs:71-86` and the engine `LoginManager` | Base address of the login service. The code appends `/api/Account/FindUser`. The engine calls it when an automation sends a username and password. |

## Logging

| Key | Default | Env override | Read by | Effect |
| --- | --- | --- | --- | --- |
| `Logging:LogLevel:Default` | `Error` | `Logging__LogLevel__Default` | `g4-abstraction\src\G4.Abstraction.Logging\G4LoggerSettings.cs:33` | Minimum level for categories not listed below. |
| `Logging:LogLevel:G4.Api` | `Information` | `Logging__LogLevel__G4.Api` | same | Level for the `G4.Api` logger, the logger the Hub uses. |
| `Logging:LogLevel:Microsoft.AspNetCore` | `Warning` | `Logging__LogLevel__Microsoft.AspNetCore` | ASP.NET Core logging | Framework noise filter. |
| `Logging:G4Logger:AddConsole` | `false` | `Logging__G4Logger__AddConsole` | `G4LoggerSettings.cs:40` and `g4-engine\src\G4.Extensions\G4Utilities.cs:208-209` | Also write logs to the console. The code default is `true`. |
| `Logging:G4Logger:AddDebug` | `false` | `Logging__G4Logger__AddDebug` | same | Also write logs to the debug output. The code default is `true`. |
| `Logging:G4Logger:OutputDirectory` | `data` | `Logging__G4Logger__OutputDirectory` | `G4LoggerSettings.cs:34, 46-48` and `G4Logger.cs:351-377` | Folder for the log files. `.` or an empty value means the working directory. |
| `Logging:G4Logger:Type` | `Text` | `Logging__G4Logger__Type` | `G4Logger.cs:131-137` | Line format: `Text` (default), `Json` or `Simple`. `Xml` is accepted but writes a "not implemented" message instead of the entry. |

The log file is `G4.Api.<yyyyMMdd>.log` in the output directory. Source: `G4Logger.cs:377`.

## OpenAI

The OpenAI settings feed the OpenAI proxy and the `ResolveLocator` MCP tool. Each environment variable wins over the file when it is set and not blank. Source: `src\G4.Settings\AppSettings.cs:154-220`.

| Key | Default | Env override | Read by | Effect |
| --- | --- | --- | --- | --- |
| `OpenAi:ApiKey` | empty | `OPENAI_API_KEY` | `AppSettings.cs:182` | The API key sent to the provider. |
| `OpenAi:ClientOptions:Endpoint` | `https://api.openai.com/v1` | `OPENAI_API_ENDPOINT` | `AppSettings.cs:171` | Base address of an OpenAI-compatible API. An empty value stores `null`. |
| `OpenAi:ClientOptions:NetworkTimeout` | `00:01:00` | `OPENAI_NETWORK_TIMEOUT` | `AppSettings.cs:193` | Timeout of each request. |
| `OpenAi:ClientOptions:OrganizationId` | `null` | `OPENAI_ORGANIZATION_ID` | `AppSettings.cs:198` | Sent to scope requests to an organization. |
| `OpenAi:ClientOptions:ProjectId` | `null` | `OPENAI_PROJECT_ID` | `AppSettings.cs:203` | Sent to scope requests to a project. |
| `OpenAi:ClientOptions:RetryPolicy:MaxRetries` | `3` | `OPENAI_MAX_RETRIES` | `AppSettings.cs:177` | Retries for transient failures. The env value is used only when the file has no `RetryPolicy`. |
| `OpenAi:ClientOptions:UserAgentApplicationId` | `null` | `OPENAI_USER_AGENT_APPLICATION_ID` | `AppSettings.cs:213` | Application id added to the user agent. |

## SignalR tuning

These values are fixed in code, not read from `appsettings.json`. Source: `src\G4.Settings\Models\HubOptionsSettings.cs`.

| Property | Default | Effect |
| --- | --- | --- |
| `ClientTimeoutInterval` | 60 seconds | How long the server waits for a client message before it closes the connection. |
| `KeepAliveInterval` | 15 seconds | Interval of the server keep-alive ping. |
| `EnableDetailedErrors` | `true` | Sends exception details to clients. |
| `MaximumReceiveMessageSize` | `long.MaxValue` | Largest incoming hub message. |
| `StatefulReconnectBufferSize` | 100,000 bytes | Buffer per connection for stateful reconnect. |

## Ports

| Port | Used by | Source |
| --- | --- | --- |
| 9944 | G4 Hub | `src\G4.Services.Hub\appsettings.json:32`, `docker\G4.Services.Hub.Dockerfile` (`EXPOSE 9944`) |
| 9955 | G4 Worker | `src\G4.Services.Worker\appsettings.json:5` |
| 9955 | UIA recorder (collision with the Worker) | `g4-recorders\src\G4.Recorders.Uia\appsettings.json:14` |
| 9956 | Chromium recorder | `g4-recorders\src\G4.Recorders.Chromium\appsettings.json:7` |
| 9957 | SAP recorder | `g4-recorders\src\G4.Recorders.Sap\appsettings.json:7` |
| 4444 | Selenium Grid, as started by the release workflow | `.github\workflows\release-pipline.yml` (step "Run Selenium Grid Docker Container") |
| 5555 | UIA driver server | `uia-driver-server\README.md:134` |

> **💡 Tip:** The Worker and the UIA recorder both default to 9955. Do not run them on the same machine with default settings. Change one with `Kestrel__Endpoints__Http__Url`.

Next: follow the [install and run path](install-and-run/README.md).
