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

The Android recorder also sends `InvokeLongClick` and `SwitchActivity`, and the Hub gives each the argument its plugin
reads:

| Event | Rule | Argument |
| --- | --- | --- |
| `InvokeLongClick` | `InvokeLongClick` on the recorded element | `{{$ --Duration:<ms>}}` from `value.durationMilliseconds`, rounded to whole milliseconds. Without a positive value the argument is empty and the plugin holds for its 1000 ms default. |
| `SwitchActivity` | `SwitchActivity`, with no `onElement` | `{{$ --Package:<package> --Activity:<activity>}}` from `value.package` and `value.activity`. Without a package only `--Activity` is written. |

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

## Locator candidates on Chromium element rules

The Chromium recorder ranks several locators for the target of an element event and sends them on the trigger node's `properties`. The Hub copies four of them into the rule's `capabilities`, next to `elementName` and the DOM context: `locators` (the ranked candidates), `locatorStrength` (`Strong`, `Fair` or `Weak`, for the chosen locator), `shadowPath` (the locator steps that lead through shadow roots to the target, when it is inside one) and `originalLocator` (the absolute XPath of the node the pointer landed on, when the recorder recorded the click against the surrounding control instead):

```json
{
    "$type": "Action",
    "pluginName": "InvokeClick",
    "onElement": "//button[normalize-space()='Save']",
    "capabilities": {
        "elementName": "Save",
        "locators": [
            { "using": "Xpath", "value": "//button[normalize-space()='Save']", "strategy": "RoleName", "score": 92, "verdict": "Unique" },
            { "using": "CssSelector", "value": "[data-test-id=\"save\"]", "strategy": "TestHook", "score": 78, "verdict": "Unique" }
        ],
        "locatorStrength": "Strong",
        "shadowPath": [
            { "using": "Xpath", "value": "//*[@id='host']" },
            { "using": "CssSelector", "value": "button" }
        ],
        "originalLocator": "/html[1]/body[1]/form[1]/div[3]/button[1]"
    }
}
```

- Only rules that target an element get them. Navigation and other page-level rules never do.
- Each key is written only when the recorder sent a value: a non-empty array for `locators` and `shadowPath`, a non-empty string for `locatorStrength` and `originalLocator`. A rule whose trigger node has none of them is unchanged.
- `onElement` stays the recorder's best XPath, and the rule has no `locator` type, so the engine reads it as XPath. A client can replace it with another candidate from `locators`. The one exception is a target inside a shadow root, described below.
- The recorder can correct an event it has already sent: after it re-tests the locators against the live page, it broadcasts `ReceiveLocatorAmendment` with the event's `locatorId`. The Hub then updates the buffered event's locator, `locators` and `locatorStrength`, so the stopped flow carries the corrected values. An amendment for an unknown `locatorId`, or one that arrives after stop, is ignored. A suspended recorder still takes amendments for events it buffered before the suspension.
- Replay does not use them. Unlike the DOM context, they are small, and the VS Code extension keeps them in a saved flow.

For a target inside a shadow root, open or closed, an XPath reaches only the outermost shadow host. The recorder still sends that host XPath as `chain.locator`, and adds one `ShadowPath` candidate to `locators`: a CSS selector that chains one segment for each shadow root with `>>>`. The Hub writes the `value` of that candidate as the rule's `onElement` and sets the rule's `locator` to `CssSelector`, and the engine walks one shadow root per segment:

```json
{
    "$type": "Action",
    "pluginName": "InvokeClick",
    "onElement": "#lab-shadow-host >>> button[name='save']",
    "locator": "CssSelector",
    "capabilities": {
        "elementName": "Save",
        "locators": [
            { "using": "CssSelector", "value": "#lab-shadow-host >>> button[name='save']", "strategy": "ShadowPath", "score": 68, "verdict": "Unique" },
            { "using": "Xpath", "value": "//*[@id='lab-shadow-host']", "strategy": "Id", "score": 75, "verdict": "Unique" }
        ]
    }
}
```

- The Hub takes the first candidate in list order whose `strategy` is `ShadowPath`, whose `using` is `CssSelector`, whose `value` is not empty and whose `verdict` is not `Broken`.
- The host XPath of `chain.locator` is no longer the rule's `onElement`, but it stays in `capabilities.locators`, and the other capabilities are copied as before.
- A `Broken` shadow path is ignored: the rule falls back to the host XPath and has no `locator` type.

## Drag and drop

A recorded drag and drop becomes one rule. The dragged element is the rule's `onElement`, and the argument says where the drag ends.

| Recorder | Event | Rule |
| --- | --- | --- |
| Chromium | `InvokeDragAndDrop`, with the drop element's chain in `value.target.chain` and the distance from the source center to the release point in `value.deltaX` and `value.deltaY` | `InvokeDragAndDrop` |
| UIA | `Left Up` that carries `value.drag`, in every mode | `InvokeUser32DragAndDrop` |

```json
{
    "$type": "Action",
    "pluginName": "InvokeDragAndDrop",
    "onElement": "//li[normalize-space()='Card 1']",
    "argument": "{{$ --Target://ul[@aria-label='Done']}}",
    "capabilities": {
        "elementName": "Card 1",
        "target": {
            "elementName": "Done",
            "locators": [
                { "using": "Xpath", "value": "//ul[@aria-label='Done']", "strategy": "AriaLabel", "score": 95, "verdict": "Unique" }
            ],
            "locatorStrength": "Strong"
        }
    }
}
```

- For Chromium, the drop element is the first candidate the argument can carry: its usable `ShadowPath` candidate, then its chain XPath, then its other candidates that are not `Broken`. Any type other than XPath is written as `--TargetLocator`.
- A locator the argument cannot carry is skipped. That is a locator with whitespace followed by `--` and a name character, which the CLI parser reads as a new argument, or with `{{$`, which it reads as a nested expression.
- Without a usable drop element, the rule replays the distance: `{{$ --DeltaX:<x> --DeltaY:<y>}}`, rounded to whole pixels.
- `capabilities.target` carries the drop element's name and the same locator capabilities as the dragged element, so a client can swap or heal the drop locator.
- A locator amendment whose `locatorId` names the drop element corrects `value.target.chain`, and the event counts once.
- For UIA, `value.drag` carries the press point (`startX`, `startY`), the grab offset from the source's top-left corner (`sourceOffsetX`, `sourceOffsetY`), and the drop element's chain (`target`) with its offset from that element's top-left corner (`targetOffsetX`, `targetOffsetY`).
- Each UIA end is an element point when its element has a locator, and a screen point otherwise. The UIA target locator must also fit in the argument.

    | End | Element | No usable element |
    | --- | --- | --- |
    | Start | `onElement`, plus the grab offset | `--X --Y`, the press point |
    | End, element start | `--Target`, plus the drop offset | `--DeltaX --DeltaY` from the press point |
    | End, screen-point start | `--Target`, plus the drop offset | `--TargetX --TargetY`, the release point |

- An element start with no usable target ends by its distance, so the drop follows the element when the window moves.
- A UIA drag without a chain, pressed over nothing UI Automation resolved, is kept and starts at the press point.
- In UIA `coordinate` mode both ends are screen points and the rule has no `onElement`: `{{$ --X --Y --TargetX --TargetY}}`. A screen point replays correctly only on the same screen layout.
- A measured offset pair becomes `--Alignment:TopLeft --OffsetX --OffsetY`, and for the target `--TargetAlignment:TopLeft --TargetOffsetX --TargetOffsetY`. The corner is the reference that every UIA driver version applies. An offset the recorder could not measure is absent, and that end keeps the element center.

## Scroll

A recorded `InvokeScroll` event becomes an `InvokeScroll` rule that scrolls by a pixel offset from the current position.
The rule keeps the recorded element as its `onElement`, and its argument is `{{$ --Left:<x> --Top:<y> --Relative}}`:

```json
{
    "$type": "Action",
    "pluginName": "InvokeScroll",
    "onElement": "//main",
    "argument": "{{$ --Left:0 --Top:240 --Relative}}"
}
```

- `--Relative` makes the plugin scroll by `--Left` and `--Top` pixels, not to them. A positive `--Top` scrolls down and
  a positive `--Left` scrolls right.
- The Chromium recorder sends one event for each wheel event, with that event's pixel deltas in `value.deltaX` and
  `value.deltaY`. The Hub uses them as `--Left` and `--Top`, rounded to whole pixels. They are not multiplied by
  `value.notches`, because the notch count is derived from the same delta. A delta that is not a JSON number counts as
  zero.
- Without numeric deltas, the Hub scrolls 120 pixels per notch along `value.direction`. For example, `Down` with 2
  notches becomes `--Left:0 --Top:240`, and `Left` with 2 notches becomes `--Left:-240 --Top:0`.
- A missing direction scrolls down, and a missing or unusable notch count scrolls one notch.
- The Android recorder sends only `direction` and `notches`, so its scroll rules use the notch mapping.
- `InvokeScroll` runs JavaScript in the page, so the rule replays on web pages only. An Android scroll rule therefore
  does not scroll a native app screen.

## Clicks and keys from the UIA recorder

The UIA recorder sends raw button and key releases, and the Hub picks the plugin from the release and the recorder's
`mode`:

| Release | `standard` mode | `user32` and `coordinate` modes |
| --- | --- | --- |
| `Left Up` | `InvokeClick` | `InvokeUser32Click` |
| `Middle Up` | `InvokeMiddleClick` | `InvokeUser32MiddleClick` |
| `Right Up` | `InvokeContextClick` | `InvokeUser32ContextClick` |
| A discrete key, such as `Enter` | `SendKeyboardKey` | `SendUser32KeyboardKey` |
| A shortcut chord, such as Ctrl+A | `SendUser32KeyboardKey` | `SendUser32KeyboardKey` |

- A click targets the recorded element, except in `coordinate` mode, where it replays at the screen point as
  `{{$ --X --Y}}`.
- A `Left Up` that carries `value.drag` is a drag and drop, described above, not a click.
- `SendUser32KeyboardKey` sends its key to the focused window, so its rule has no `onElement` and no
  `capabilities.elementName`, in every mode. Only `SendKeyboardKey` keeps the recorded element.
- The Chromium recorder sends a middle click (`auxclick` with button 1) as `InvokeMiddleClick`, which becomes an
  `InvokeMiddleClick` rule on the recorded element.

## Status codes

An empty recording returns `204 No Content` from stop and flow retrieval.
Retrieval before stop returns `409 Conflict`, and an unknown ID returns `404 Not Found`.
Invalid settings, and suspending a recorder that is not part of the session, return `400 Bad Request`.
Suspending or resuming a session that is not recording returns `409 Conflict`.
When every recorder fails to start, start returns `502 Bad Gateway`; a partial start returns the session with
per-recorder errors.
The recordings HTTP examples cover the complete lifecycle.
