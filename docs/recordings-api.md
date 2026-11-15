# Recording Automations

The Hub connects to running G4 recorder services and turns their events into one native G4 automation flow.
The usual recorder endpoints are UIA on port `9955`, Chromium on port `9956` and Android on port `9958`.
Recording sessions live in memory until the Hub stops; they are not persisted.

| Route | Result |
| --- | --- |
| `POST /api/v4/g4/recordings` | Starts the enabled recorders and returns the session ID and recorder diagnostics. |
| `GET /api/v4/g4/recordings/{id}` | Returns the lifecycle state and each recorder's connection, event count, suspension, and errors. After stop, `jobs` maps each job to its recorder. |
| `POST /api/v4/g4/recordings/{id}/suspend` | Drops one recorder's events until resumed; the body is `{ "baseUrl": "..." }`. |
| `POST /api/v4/g4/recordings/{id}/resume` | Resumes buffering for a suspended recorder; the body is `{ "baseUrl": "..." }`. |
| `POST /api/v4/g4/recordings/{id}/stop` | Stops the session and returns the automation; a repeated stop returns the same result. |
| `GET /api/v4/g4/recordings/{id}/flow` | Returns the completed automation without a metadata wrapper. |

The start body can also carry `excludedProcessIds` and `excludedWindowTitles`.
UIA events from those processes, or from windows whose title contains one of those fragments, are left out of the
automation.
A client that records from inside its own window, such as the VS Code recorder panel, uses them to skip its own
clicks.

## Recorder drivers

The `driverParameters.driver` of each recorder picks how the Hub talks to it:

| Driver | Start and stop | Events |
| --- | --- | --- |
| `UiaDriver` | The Hub only subscribes; the recorder captures the whole desktop. | Raw input, which the Hub classifies by `mode`. |
| Any name that contains `chrome` | The Hub calls `StartRecorder` with the `driverParameters`, which launches a browser, and `StopRecorder` on stop. | Plugin names, such as `InvokeClick`. |
| `AndroidDriver` | The Hub calls `StartRecorder` with the `driverParameters`, which prepares the device named by `appium:udid` and starts `appium:appPackage`, and `StopRecorder` on stop. | Plugin names, as for Chromium. |

For an Android recorder, the `driverParameters` are also what the flow replays with: `driver` is `AndroidDriver`, `driverBinaries` is the Appium server endpoint (for example `http://localhost:4723`), and `capabilities.alwaysMatch` with the `firstMatch` list next to it are the W3C WebDriver new-session capabilities, with every Appium capability under the `appium:` prefix. The Hub passes the whole object to the recorder's `StartRecorder`, which reads `appium:udid`, `appium:appPackage`, `appium:appActivity` and `appium:app`, and copies it into the flow unchanged. The recorders' Android recorder module describes the replay setup.

Chromium and Android always record in `standard` mode. Another driver is refused with
`Recorder driver must be AndroidDriver, ChromeDriver or UiaDriver.`

The Android recorder also sends `InvokeLongClick` and `SwitchActivity`, which have no replay plugin yet. The Hub keeps
them in the flow, as it keeps every recorder event. A client leaves them out with its own filter; the VS Code
extension does so through `excludeEvents`.

## DOM context on Chromium element rules

The Chromium recorder can send a snapshot of the markup around the target of an element event, as `domContext` (Base64 of UTF-8 HTML) and `domContextLength` (its decoded byte count) on the trigger node's `properties`. The Hub copies both into the rule's `capabilities`, next to `elementName`:

```json
{
    "$type": "Action",
    "pluginName": "InvokeClick",
    "onElement": "/html[1]/body[1]/form[1]/div[3]/button[1]",
    "capabilities": {
        "elementName": "Login",
        "domContext": "PGRpdiBjbGFzcz0icm93Ij48YnV0dG9uIGRhdGEtZzQtdGFyZ2V0PSJ0cnVlIj5Mb2dpbjwvYnV0dG9uPjwvZGl2Pg==",
        "domContextLength": 67
    }
}
```

- Only rules that target an element get it. Navigation and other page-level rules never do.
- A rule whose element has no name gets `capabilities` with the context alone.
- Decode the Base64 text as UTF-8 and look for the element marked `data-g4-target="true"`. A client can use it to heal or optimise the `onElement` locator.
- The Android recorder sends the same two keys, holding the XML of the accessibility subtree in the UiAutomator2 page-source vocabulary. The target carries `g4-target="true"`, and the Hub copies it the same way.
- Replay does not use them: the engine and the plugins copy `capabilities` along with the rule but read neither key. A client that does not want them in a saved flow removes them; the VS Code extension does so by default.

An empty recording returns `204 No Content` from stop and flow retrieval.
Retrieval before stop returns `409 Conflict`, and an unknown ID returns `404 Not Found`.
Invalid settings, and suspending a recorder that is not part of the session, return `400 Bad Request`.
Suspending or resuming a session that is not recording returns `409 Conflict`.
When every recorder fails to start, start returns `502 Bad Gateway`; a partial start returns the session with
per-recorder errors.
The recordings HTTP examples cover the complete lifecycle.
