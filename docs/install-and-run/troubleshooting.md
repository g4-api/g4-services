# Troubleshooting

[⬅ Back to overview](README.md)

Quick fixes for the problems you are most likely to hit while installing the Hub.

| Symptom | Cause | Fix |
| --- | --- | --- |
| The designer or another browser tool shows a CORS error | The page origin is not in `AllowedOrigins` or `ORIGINS`. The match is exact, and `ORIGINS` replaces the file when it is set | Add the exact origin, with scheme and port, to `ORIGINS` (semicolon separated) and restart the Hub. See [Module 2](02-ports-and-cors.md) |
| The Hub does not start and reports that the address is in use | Another program already uses the port, or a second Hub is running. The Worker and the UIA recorder both default to 9955 | Stop the other program, or set `Kestrel__Endpoints__Http__Url` to a free port. See [Module 2](02-ports-and-cors.md) |
| Flows or environments are gone after a restart | In Docker the data lived inside the container, and the container was removed | Mount a volume on `/app/data` and keep the same encryption key. See [Module 3](03-data-folder-and-volume.md) |
| The data cannot be opened after you changed the key | `g4-engine.db` is encrypted with the key it was created with | Put the old `G4__StateManager__DataEncryptionKey` back. See [Module 3](03-data-folder-and-volume.md) |
| The Hub ignores your `appsettings.json` edits | The Hub reads the file from the working directory, not from the folder of the `.dll` | Start the Hub from the folder that holds the file. See [Module 1](01-pick-a-mode.md) |
| A run is rejected because of the token | The engine received no token, or a token it cannot accept. The Hub itself does not check tokens | Send a valid value in `authentication.token`. See [Module 4](04-tokens.md) |
| `ping` does not answer | The Hub is not running, the port differs, or a firewall blocks it | Check the process or container, then use the port from `Kestrel:Endpoints:Http:Url`. See [Module 5](05-verify-the-hub.md) |
| No log file appears | `OutputDirectory` points to another folder than you expect | Check `Logging:G4Logger:OutputDirectory`. See [Module 6](06-logging.md) |

Still stuck? Read the log file first, then see the [configuration reference](../configuration.md).
