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

## Branding (white label)

The `Branding` section replaces the product labels and visible text of the Hub, the G4 VS Code extension, and the Test Wright extension. Only labels and text change. Routes, assembly, package, and extension names, IDs, settings keys, MCP tool and server names, and data values such as the `G4.System` namespace never change. Every value defaults to the current G4 text, so a Hub without the section looks the same as before. Source: `src\G4.Services.Domain.V4\Extensions\BrandingExtensions.cs` (`NewBranding`), model in `src\G4.Services.Domain.V4\Models\BrandingModels.cs`.

| Key | Default | Env override | Effect |
| --- | --- | --- | --- |
| `Branding:ProductName` | `G4` | `Branding__ProductName` | The base name that replaces "G4". Every empty name below is derived from it. |
| `Branding:TrademarkSymbol` | `™` | `Branding__TrademarkSymbol` | Placed after the product name in marked names, for example "G4™ Engine". An empty string removes it. |
| `Branding:Names:Engine` | `<ProductName> Engine` | `Branding__Names__Engine` | The engine name. |
| `Branding:Names:EngineClient` | `<ProductName> Engine Client` | `Branding__Names__EngineClient` | The name of the VS Code extension that hosts the designer. |
| `Branding:Names:Hub` | `<ProductName> Hub` | `Branding__Names__Hub` | The Hub name. |
| `Branding:Names:Studio` | `<ProductName> Studio` | `Branding__Names__Studio` | The studio name. |
| `Branding:Names:TestWright` | `<ProductName> Test Wright` | `Branding__Names__TestWright` | The test workbench name. |
| `Branding:Tagline` | `Automation as a Service` | `Branding__Tagline` | The console banner tagline. |
| `Branding:ProjectUrl` | `https://github.com/g4-api` | `Branding__ProjectUrl` | The project page in the console banner. |
| `Branding:Support:Name` | `<ProductName> Support` | `Branding__Support__Name` | The Swagger contact name. |
| `Branding:Support:Email` | `g4.platforms@gmail.com` | `Branding__Support__Email` | The Swagger contact email. |
| `Branding:Support:Url` | `https://github.com/g4-api/g4-docs` | `Branding__Support__Url` | The Swagger contact address. |
| `Branding:TermsUrl` | the G4 usage policy | `Branding__TermsUrl` | The Swagger terms of service. |
| `Branding:Assets:Logo` | `/images/avatar.png` | `Branding__Assets__Logo` | The page header logo: a path under `wwwroot` or an absolute URL. |
| `Branding:Assets:Favicon` | `/images/favicon.ico` | `Branding__Assets__Favicon` | The browser tab icon: a path under `wwwroot` or an absolute URL. |
| `Branding:Assets:ActivityBarIcon` | empty | `Branding__Assets__ActivityBarIcon` | The SVG icon of the G4 VS Code extension on the activity bar: a path under `wwwroot` or an absolute URL. Empty keeps the built-in double orbit. |
| `Branding:Assets:TestWrightIcon` | empty | `Branding__Assets__TestWrightIcon` | The SVG icon of Test Wright on the activity bar, its panel tab, and its page header: a path under `wwwroot` or an absolute URL. Empty keeps the built-in icon. |
| `Branding:ConsoleBanner:Mode` | `Default` | `Branding__ConsoleBanner__Mode` | `Default` writes the G4 Hub art, `Text` writes the Hub name as plain text, `File` writes the art from `File`. |
| `Branding:ConsoleBanner:File` | `null` | `Branding__ConsoleBanner__File` | The art file of the `File` mode. A missing file falls back to `Text`. |

The "Powered by G4™ Engine" credit on the designer and in the console banner, and the "G4™ Engine" footer links, always stay: the license requires the credit. Documentation, screenshots, and READMEs keep their G4 text.

**Placeholders.** The Hub resolves the settings into a `tokens` map: `product`, `productTm`, `hub`, `hubTm`, `engine`, `engineTm`, `engineClient`, `engineClientTm`, `studio`, `studioTm`, `testWright`, `testWrightTm`, and `tm`. Text written for the Hub, the pages, and the extensions names a product through a placeholder such as `{engineTm}`. Only these placeholders are replaced, so other braces, such as route templates, stay as written. A `Tm` placeholder places the trademark symbol right after the product name, or after the name when it does not contain the product name.

**Where the branding is published.**

| Channel | Who reads it |
| --- | --- |
| `GET /api/v4/g4/integration/branding` | The G4 VS Code extension and Test Wright, when they connect. They keep a copy for the next start. |
| `GET /api/v4/g4/integration/branding.js` | The Hub pages, before any other script (`components/g4-branding/g4-branding.js` applies it). |
| `wwwroot/branding.json` | Written at startup. Clients that download the web root receive it with the other files. A read-only web root skips the file and logs a warning; the endpoints still work. |

**What follows the branding.** Swagger titles, descriptions, contact, and terms; MCP tool titles and descriptions and the server title (the server name `g4-engine-mcp` stays); the console banner; the Hub pages and the designer; and the runtime text of both extensions. Output channel names in VS Code change on the next start after a new branding.

**Static extension labels.** VS Code reads the display name, command categories and titles, view and editor names, and icons from `package.json` once, so they cannot follow the Hub at runtime. Package a branded copy of each extension instead; the script rewrites a staging copy and never changes the repository:

```text
cd g4-vscode-extension
node scripts/rebrand.mjs --branding http://localhost:9944 --out C:\vsix

cd g4-test-wright-vscode-extension
node scripts/rebrand.mjs --branding <Hub folder>\wwwroot\branding.json --out C:\vsix
```

**Extension icons.** Each extension picks its activity bar icon in this order: the local VS Code setting (`g4.sidePanelIcon` or `g4TestWright.sidePanelIcon`, a path to an `.svg` file on that machine), then `ActivityBarIcon` or `TestWrightIcon` from the Hub, then its built-in icon. The extension copies the chosen icon over its installed icon file; VS Code reads that file only at startup, so the new icon shows from the next start. The icon must be an SVG file of at most 256 KB; an invalid icon is skipped with a warning in the output channel. Draw it on a 24 by 24 grid in one color (`currentColor`), because VS Code tints the activity bar icon to the theme. An extension update restores the built-in file; the next start applies the chosen icon again. `rebrand.mjs` also packages the Hub icon into the branded copy.

The VSIX keeps the extension identity (`g4-api.g4-engine-client`, `g4-api.g4-test-wright`), so it replaces the standard extension when installed. It needs `vsce` on the PATH (`npm install -g @vscode/vsce`) or downloads it with `npx`. `--no-package` stops after the staging copy, for inspection.

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
