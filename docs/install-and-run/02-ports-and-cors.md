# Module 2: Ports and CORS

[⬅ Back to overview](README.md) · [⬅ Module 1](01-pick-a-mode.md)

Where you are: the Hub runs. About 5 minutes.

A browser tool, such as the G4 designer, can call the Hub only when the Hub allows its origin. You may also need another port when 9944 is taken.

## What you'll do

- Change the Hub port
- Allow the origins of your browser tools
- Know which other G4 services use which ports

## Steps

1. Change the port only if you need to. The default is `http://+:9944`. Set the environment variable before you start the Hub:

   ```text
   Kestrel__Endpoints__Http__Url=http://+:9950
   ```

   Or edit `Kestrel:Endpoints:Http:Url` in `appsettings.json`.

2. Allow your browser origins. The Hub allows an origin when it appears in `AllowedOrigins`, or in the `ORIGINS` environment variable. The variable replaces the file when it is set. Separate several origins with a semicolon:

   ```text
   ORIGINS=http://localhost:8000;http://localhost:5173
   ```

   The match is exact, so `http://localhost:8000` and `http://127.0.0.1:8000` are different origins.

3. Know the built-in rule. Any origin that starts with `vscode-webview://` is always allowed. G4 Studio relies on this, so you do not list it.

4. With Docker, pass the variable on the command line:

   ```text
   docker run -d -p 9944:9944 -e "ORIGINS=http://localhost:8000" --name g4-hub g4api/g4-hub:latest
   ```

5. Check the port table before you start other services on the same machine.

   | Port | Used by |
   | --- | --- |
   | 9944 | G4 Hub |
   | 9955 | G4 Worker, and also the UIA recorder |
   | 9956 | Chromium recorder |
   | 9957 | SAP recorder |
   | 4444 | Selenium Grid |
   | 5555 | UIA driver server |

   > **⚠️ Port 9955 is used twice.** The Worker and the UIA recorder both default to it. Change one of them if they run on the same machine.

## Check your work

- A browser page served from an allowed origin can call `http://localhost:9944/api/v4/g4/ping` without a CORS error in the console.
- You know which port the Hub listens on now.

## Where the facts come from

| Fact | Source |
| --- | --- |
| Default URL and key name | `src\G4.Services.Hub\appsettings.json:32` |
| `ORIGINS` replaces `AllowedOrigins`; semicolon separator | `src\G4.Services.Hub\Program.cs:102-107` |
| `vscode-webview://` always allowed | `src\G4.Services.Hub\Program.cs:114` |
| Worker 9955, UIA 9955, Chromium 9956, SAP 9957 | see the [port table](../configuration.md#ports) |

Next: [Module 3: Data folder and Docker volume](03-data-folder-and-volume.md)
