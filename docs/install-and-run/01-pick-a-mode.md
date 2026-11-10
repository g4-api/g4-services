# Module 1: Pick a mode

[⬅ Back to overview](README.md)

Where you are: the start. About 8 minutes.

The Hub ships three ways. Pick the one that fits and run it. All three listen on port 9944 by default.

## What you'll do

- Choose between release zip, Docker and source
- Start the Hub
- Confirm it is listening

## Steps

1. Choose a mode.

   | Mode | Choose it when | Needs |
   | --- | --- | --- |
   | Release zip | You want the fastest start with no build | .NET 10 runtime |
   | Docker | You want an isolated, repeatable Hub | Docker 20.10 or higher |
   | Source | You want to change the code | .NET 10 SDK and git |

2. Start the Hub in your chosen mode.

   **Release zip.** Download `g4-hub.v<version>-win-linux-osx-x64.zip` from the [Releases](https://github.com/g4-api/g4-services/releases) page, extract it, and run these commands from the extracted folder:

   ```text
   dotnet G4.Services.Hub.dll
   ```

   > **💡 Tip:** Run the command from the folder that holds `appsettings.json`. The Hub reads that file from the current working directory.

   **Docker.** Run the published image:

   ```text
   docker run -d -p 9944:9944 --name g4-hub g4api/g4-hub:latest
   ```

   To build the image yourself, clone the repository and run these commands from its root:

   ```text
   git clone https://github.com/g4-api/g4-services.git
   cd g4-services
   docker build -f ./docker/G4.Services.Hub.Dockerfile -t g4-services-hub .
   docker run -d -p 9944:9944 --name g4-services-hub g4-services-hub
   ```

   **Source.** Clone the repository and run the Hub project:

   ```text
   git clone https://github.com/g4-api/g4-services.git
   cd g4-services
   dotnet run --project ./src/G4.Services.Hub/G4.Services.Hub.csproj
   ```

3. Wait a few seconds, then open `http://localhost:9944/swagger`. The next modules explain each setting you may want to change.

## Check your work

- The command did not exit with an error.
- `http://localhost:9944/swagger` loads in a browser (Module 5 covers this in full).
- With Docker, `docker ps` lists the container as running.

## Where the facts come from

| Fact | Source |
| --- | --- |
| Target framework `net10.0` | `src\G4.Services.Hub\G4.Services.Hub.csproj:5` |
| Zip name `g4-hub.v<version>-win-linux-osx-x64.zip` | `.github\workflows\release-pipline.yml` (step "Create Build Artifact") |
| Docker image `g4api/g4-hub` | `.github\workflows\release-pipline.yml` (job `publish-hub-docker`) |
| Image exposes 9944 and starts `G4.Services.Hub.dll` | `docker\G4.Services.Hub.Dockerfile` |

Next: [Module 2: Ports and CORS](02-ports-and-cors.md)
