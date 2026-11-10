# Module 3: Data folder and Docker volume

[⬅ Back to overview](README.md) · [⬅ Module 2](02-ports-and-cors.md)

Where you are: the Hub runs and is reachable. About 5 minutes.

The Hub stores flows, environments, credentials and cache in files. If those files live inside a container with no volume, they vanish when the container is removed. This module shows where the files are and how to keep them.

## What you'll do

- Find the data folder
- Mount it as a Docker volume
- Replace the public encryption key

## Steps

1. Know the settings. `G4:StateManager:BasePath` defaults to `data`. The engine adds one more `data` folder under the base path. With the defaults, the files are here, relative to the Hub working directory:

   | File | Path |
   | --- | --- |
   | Main store (encrypted) | `data\data\g4-engine.db` |
   | Credentials store (SQLite) | `data\Data\g4-data.db` |
   | Log files | `data\G4.Api.<yyyyMMdd>.log` |

   > **💡 Tip:** The credentials folder is spelled `Data` with a capital `D`. On Linux, `data` and `Data` are two different folders.

2. In Docker, the working directory is `/app`, so the folder is `/app/data`. Mount a volume there:

   ```text
   docker run -d -p 9944:9944 -v g4-hub-data:/app/data --name g4-hub g4api/g4-hub:latest
   ```

   Or mount a host folder:

   ```text
   docker run -d -p 9944:9944 -v "$(pwd)/data:/app/data" --name g4-hub g4api/g4-hub:latest
   ```

   One volume on `/app/data` keeps the stores and the logs, because all three live under it.

3. Replace the encryption key before you store real data. The committed key is public. Set your own as an environment variable:

   ```text
   G4__StateManager__DataEncryptionKey=<your-own-secret-value>
   ```

   > **⚠️ Choose the key once.** `g4-engine.db` is encrypted with it. If you change the key later, the existing file can no longer be opened. Keep the key in a secret store, not in the image.

4. To move the base path, set `G4__StateManager__BasePath` to another folder. An empty value or `.` uses `<working directory>\data`.

## Check your work

- After you stop and start the container with the volume, the data is still there.
- `g4-engine.db` exists under the folder you mounted.
- The key in use is not the committed one.

## Where the facts come from

| Fact | Source |
| --- | --- |
| `BasePath` is `data`, key is committed in the file | `src\G4.Services.Hub\appsettings.json:17-19` |
| `data` subfolder and `g4-engine.db` | `g4-engine\src\G4.Cache\CacheManager.cs:1221-1236` |
| `Data` subfolder and `g4-data.db` | `g4-engine\src\G4.Cache\CacheManager.cs:1249-1254` |
| Empty key falls back to the committed value | `g4-engine\src\G4.Cache\CacheManager.cs:1219, 1227-1229` |
| Working directory `/app` | `docker\G4.Services.Hub.Dockerfile` (`WORKDIR /app`) |

Next: [Module 4: Tokens](04-tokens.md)
