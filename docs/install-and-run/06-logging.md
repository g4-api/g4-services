# Module 6: Logging

[⬅ Back to overview](README.md) · [⬅ Module 5](05-verify-the-hub.md)

Where you are: the Hub is verified. About 5 minutes.

The Hub writes its own log files. They are the first place to look when a request fails.

## What you'll do

- Find the log file
- Turn on console output
- Change the level or the format

## Steps

1. Find the file. With the default settings, the Hub writes `G4.Api.<yyyyMMdd>.log` to the folder named by `Logging:G4Logger:OutputDirectory`. The default is `data`, relative to the working directory. In Docker that is `/app/data`, so the volume from Module 3 also keeps the logs.

2. Show logs in the console while you debug. Console and debug output are off in the shipped file:

   ```text
   Logging__G4Logger__AddConsole=true
   ```

3. Change how much is written. The shipped file sets `Default` to `Error` and `G4.Api` to `Information`. To see more, raise the default:

   ```text
   Logging__LogLevel__Default=Information
   ```

4. Change the line format with `Logging:G4Logger:Type`. `Text` is the default. `Json` writes one JSON object per line. `Simple` writes a time, a level and the message. `Xml` is not implemented and writes a notice instead of the entry.

5. Move the files with `Logging:G4Logger:OutputDirectory`. A value of `.` or an empty value means the working directory.

## Check your work

- The log file exists and grows when you call the Hub.
- With `AddConsole` on, the same lines appear in the terminal.

## Where the facts come from

| Fact | Source |
| --- | --- |
| Defaults in the shipped file | `src\G4.Services.Hub\appsettings.json:36-48` |
| File name `G4.Api.<yyyyMMdd>.log` | `g4-abstraction\src\G4.Abstraction.Logging\G4Logger.cs:377` |
| `Text`, `Json`, `Simple`, `Xml` handling | `g4-abstraction\src\G4.Abstraction.Logging\G4Logger.cs:131-137` |
| `.` or empty output directory | `g4-abstraction\src\G4.Abstraction.Logging\G4LoggerSettings.cs:46-48` |

Next: [Troubleshooting](troubleshooting.md)
