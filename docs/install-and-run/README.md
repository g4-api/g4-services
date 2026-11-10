# Install and Run the G4 Hub

This path takes you from nothing to a G4 Hub that answers requests, with the right ports, data folder, token handling and logs.

Plan for about 30 minutes.

---

## Who is this for?

Anyone who needs to run their own G4 Hub: a tester who wants a local Hub, a developer who wants it in Docker, or an operator who sets it up for a team. You should be comfortable running a command in a terminal.

## What you'll do

By the end you will have:

- Started the Hub from a release zip, from Docker, or from source
- Opened the Hub to the browser origins that need it
- Kept the Hub data across restarts
- Understood where the G4 token goes, and what the Hub itself does not check
- Checked the Hub with the ping route and Swagger
- Found the Hub log files

## Prerequisites

- A Windows, Linux or macOS machine
- For the release zip: the .NET 10 runtime. For source builds: the .NET 10 SDK
- For Docker: Docker 20.10 or higher

> **💡 Tip:** Every setting named in this path is listed in the [configuration reference](../configuration.md).

---

## Modules

Work through these in order. Each one builds on the previous.

| # | Module | What you'll do | Time |
| --- | --- | --- | --- |
| 1 | [Pick a mode](01-pick-a-mode.md) | Start the Hub from a release zip, Docker or source | 8 min |
| 2 | [Ports and CORS](02-ports-and-cors.md) | Change the port and allow your browser origins | 5 min |
| 3 | [Data folder and Docker volume](03-data-folder-and-volume.md) | Keep the data across restarts and set your own key | 5 min |
| 4 | [Tokens](04-tokens.md) | Learn where the token goes and what the Hub checks | 4 min |
| 5 | [Verify the Hub](05-verify-the-hub.md) | Call the ping route and open Swagger | 3 min |
| 6 | [Logging](06-logging.md) | Find, read and tune the log files | 5 min |

Stuck? See [Troubleshooting](troubleshooting.md).

Back to the [Hub documentation](../README.md).
