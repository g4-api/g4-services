# Scale out with Workers and grids

[⬅ Back to the Hub documentation](README.md)

This page explains how to run automations on more than one machine. A Worker takes queued automations from the Hub. A Selenium grid supplies the browsers. You can run all three in Docker with the compose file in `docker\docker-compose.yml`.

## Sync or async invoke

The Hub has two ways to start an automation.

| | Sync | Async |
| --- | --- | --- |
| Endpoint | `POST /api/v4/g4/automation/invoke` | `POST /api/v4/g4/automation/async/start` |
| Who runs it | The Hub process itself. | Any Worker that polls the Hub. |
| Answer | The automation response, when the run ends. | `202 Accepted` with an empty body. The run is only queued. |
| Get the result | In the same response. | `GET /api/v4/g4/automation/async/completed`. |
| Live progress | Only if you set `SignalR.ConnectionId`. | Only if you set `SignalR.ConnectionId`. |

Source: `src\G4.Services.Hub.Api.V4\Controllers\AutomationController.cs` (`invoke`), `AutomationAsyncController.cs` (`start`, `completed`). The async `start` action rejects an unreadable `dataSource` with `400` before it queues anything.

The completed list is bounded. When it holds 100 entries or more, the client removes the oldest ones, by end time, down to 100. Read the result soon after the run ends. Source: `g4-api-client\src\G4.Api\Clients\AutomationAsyncClient.cs` (`AddCompletedEntry`).

## How a Worker takes work

1. The Worker connects to the orchestrator hub at `<hub>/hub/v4/g4/orchestrator`.
2. It sends `SendHeartbeat` every 10 seconds.
3. Once a second, while it is idle, it calls `GetPendingAutomation`.
4. The Hub answers with `ReceivePendingAutomation`: the next queued automation, or nothing.
5. The Worker runs the automation with its own engine.
6. The Worker calls `CompleteAutomation` with the key and the response, and the Hub answers `ConfirmAutomationResponse`.

A Worker locks itself while it runs an automation, so one Worker runs one automation at a time. To run more in parallel, start more Workers.

Source: `src\G4.Services.Domain.V4\G4HubListener.cs` (`StartG4HubListener`, `ReceivePendingAutomation`, `StartHeartbeatListener`), `src\G4.Services.Domain.V4\Hubs\G4Hub.cs`. The events are described in [Monitor runs live over SignalR](signalr.md).

> **💡 Tip:** The polling interval is fixed in code (`TODO: Control interval with a configuration setting` in `G4HubListener.cs`). You cannot change it with a setting.

## Start a Worker

A Worker finds the Hub address in this order:

| Order | Source | Example |
| --- | --- | --- |
| 1 | The `HUB_URI` environment variable. | `HUB_URI=http://hub-host:9944` |
| 2 | `G4:WorkerConfiguration:HubUri` in the Worker `appsettings.json`. | `http://localhost:9944` |
| 3 | The built-in default. | `http://localhost:9944` |

Source: `G4HubListener.cs` (`ResolveHubUri`).

The `run` command sets `HUB_URI` for you:

```text
G4.Services.Worker run --hub http://hub-host:9944
```

Source: `src\G4.Services.Worker\Cli\RunCommand.cs` (the `hub` parameter). `src\G4.Services.Worker\Program.cs` calls the command and then starts the listener.

The Worker listens on port 9955 by default (`src\G4.Services.Worker\appsettings.json:5`). The UIA recorder also defaults to 9955, so do not run both on one machine with default settings. See the port table in [Configuration](configuration.md).

## Max parallel

`settings.automationSettings.maxParallel` controls how many data rows one automation runs at the same time. The default is 1, and any value below 1 counts as 1. It applies to the sync invoke path. The async client has the code that would read it commented out, and the Worker calls the queue-model overload of `Invoke`, which does not read it.

Source: `g4-engine\src\G4.Models\SettingsModels.cs:89, 107`, `g4-api-client\src\G4.Api\Extensions\LocalExtensions.cs` (`GetMaxParallel`), `AutomationClient.cs` (`Invoke`), `AutomationAsyncClient.cs:716-718` (commented out).

> **⚠️ Warning:** Do not rely on `maxParallel` to spread work over Workers. Parallel Workers come from starting more Workers, not from this setting.

## Use a Selenium grid

Set `driverBinaries` to the grid address. A value that starts with `http://` or `https://` makes the driver remote. Anything else starts a local driver.

```json
{
    "driverParameters": [
        {
            "driver": "ChromeDriver",
            "driverBinaries": "http://chrome:4444"
        }
    ]
}
```

Source: `g4-abstraction\src\G4.Abstraction.WebDriver\DriverFactory.cs:137-140`, and [the driver table](../../g4-abstraction/docs/drivers.md). The release workflow uses `http://localhost:4444` as the grid address (`GRID_ENDPOINT` in `.github\workflows\release-pipline.yml:205`) and starts the grid with `SE_NODE_MAX_SESSIONS=5`.

Use the address from where the driver runs:

| Driver runs | Grid address |
| --- | --- |
| In the Hub or Worker container of the compose file | `http://chrome:4444` |
| On the host, grid published on port 4444 | `http://localhost:4444` |
| On another machine | `http://<grid-host>:4444` |

## Run everything with Docker Compose

A compose file starts three services. The full text is below. Save it as `docker-compose.yml` in the `docker` folder of the repository. The repository does not ship this file.

| Service | Image or build | Port | Notes |
| --- | --- | --- | --- |
| `hub` | `g4api/g4-hub:latest` | 9944 | Data folder `./data` mounted at `/app/data`. `ORIGINS` is set for the two local designer origins. |
| `worker` | Built from `docker\G4.Services.Worker.Dockerfile` | 9955 | `HUB_URI=http://hub:9944`. No Worker image is published, so compose builds it. |
| `chrome` | `selenium/standalone-chrome:latest` | 4444 | `shm_size` 2 GB, `SE_NODE_MAX_SESSIONS=5`, `SE_NODE_OVERRIDE_MAX_SESSIONS=true`. |

Image names come from the `publish-hub-docker` job of `.github\workflows\release-pipline.yml`. The grid settings are copied from its "Run Selenium Grid Docker Container" step.

1. Save the file below as `docker-compose.yml` and open a terminal in that folder (the `docker` folder of the repository).
2. Run `docker compose up --build`.
3. Check the Hub at `http://localhost:9944/api/v4/g4/ping`. See [Verify the Hub](install-and-run/05-verify-the-hub.md).
4. Queue an automation with `POST http://localhost:9944/api/v4/g4/automation/async/start`. Use `http://chrome:4444` as `driverBinaries`.
5. After a few seconds, read the result at `http://localhost:9944/api/v4/g4/automation/async/completed`.

The compose file is untested as a whole stack. The syntax was checked with `docker compose config` (Docker 29.7.2). The images were not pulled, the Worker image was not built, and the stack was not started.

```text
# Runs the G4 Hub, one G4 Worker and a Selenium Chrome grid on one machine.
# Start it from this folder with: docker compose up --build
services:
  hub:
    # The Hub image is published by the release workflow (publish-hub-docker job).
    image: g4api/g4-hub:latest
    ports:
      - "9944:9944"
    volumes:
      # The Hub stores its data under /app/data (WORKDIR /app in the Dockerfile).
      - ./data:/app/data
    environment:
      # Semicolon separated browser origins; replaces AllowedOrigins in appsettings.json.
      ORIGINS: "http://localhost:8000;http://localhost:5173"

  worker:
    # No Worker image is published, so build it from the repository root.
    build:
      context: ..
      dockerfile: docker/G4.Services.Worker.Dockerfile
    depends_on:
      - hub
      - chrome
    ports:
      - "9955:9955"
    environment:
      # The Worker connects to the Hub orchestrator hub at this address.
      HUB_URI: "http://hub:9944"

  chrome:
    # Same image and settings as the "Run Selenium Grid Docker Container" step of the release workflow.
    image: selenium/standalone-chrome:latest
    shm_size: "2gb"
    ports:
      - "4444:4444"
    environment:
      SE_NODE_MAX_SESSIONS: "5"
      SE_NODE_OVERRIDE_MAX_SESSIONS: "true"
```

## Ports

| Port | Used by |
| --- | --- |
| 9944 | Hub |
| 9955 | Worker |
| 4444 | Selenium grid |

The full list, with recorders and driver servers, is in [Configuration](configuration.md).

## If it fails

| Symptom | Cause | Fix |
| --- | --- | --- |
| `202` from `async/start`, but nothing runs | No Worker is connected, or the Worker points at the wrong Hub. | Check `HUB_URI` on the Worker. In compose it must be `http://hub:9944`, not `localhost`. |
| Two Workers, and one stays idle | One Worker takes one automation at a time, and the queue is short. | Queue more automations. |
| `completed` does not list an old run | The list keeps 100 entries and removes the oldest. | Read results sooner. |
| The Worker container exits at start | The build or the Hub address failed. | Run `docker compose logs worker`. |
| The browser cannot start | The grid is not ready, or `driverBinaries` has the wrong host. | Open `http://localhost:4444/status`. Use `http://chrome:4444` inside compose. |
