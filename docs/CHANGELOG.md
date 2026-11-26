# Changelog

All notable user-visible changes to the G4 Hub are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## 2026.11.26.92

### Added

- Recordings accept the Android recorder (`AndroidDriver`, port 9958): the Hub starts and stops it through `StartRecorder` and `StopRecorder`, and converts its events with the Chromium rule builder.
- Recorded Chromium element rules carry the recorder's Base64 DOM context in `capabilities.domContext` and `capabilities.domContextLength`.
- Recorded Chromium element rules carry the recorder's ranked locator candidates in `capabilities.locators`, `capabilities.locatorStrength`, `capabilities.shadowPath` and `capabilities.originalLocator`.
- Recorded Chromium element rules whose target is inside a shadow root use the recorder's `ShadowPath` candidate as `onElement`, with `locator` set to `CssSelector`; a `Broken` shadow path falls back to the host XPath.
- The Hub applies the recorder's `ReceiveLocatorAmendment` corrections to events it has already buffered.
- Recorded drag and drop: a Chromium `InvokeDragAndDrop` event becomes an `InvokeDragAndDrop` rule that drops on the
  target's locator or by the recorded distance, with the target's candidates in `capabilities.target`. A UIA `Left Up`
  that carries `value.drag` becomes `InvokeUser32DragAndDrop` in every mode, with element points where a locator exists
  and screen points (`--X --Y`, `--TargetX --TargetY`) otherwise; coordinate mode uses screen points only. Locator
  amendments also correct the drop element (000b935, 67ee706).
- Input Actions Groups in the designer: input actions (plugin type `InputAction`) are listed in the toolbox, an input
  action must sit directly inside an Input Actions Group (`InvokeInputActions`) and a group may hold only input actions,
  both as errors, and an empty group is a warning (197f291).
- Parallel containers: a `Parallel` container runs stages at the top level and jobs inside a Stage, and the designer
  validates its placement (c5c15a4).
- A recorded UIA `Middle Up` release becomes `InvokeMiddleClick` in `standard` mode and `InvokeUser32MiddleClick` in the
  other modes.
- Recordings domain and an MCP server built on the official SDK (bc38142).
- MCP tool resolution (11a8d55).
- Namespace-aware tools and toolbox filtering (c65ecea).
- The `SendRules` tool runs a batch of rules in one call (6658bbf).
- The `RegisterCapability` tool (dfb1ad2).
- Framed, mutation-aware scrollbars in the smart editor (204ec5c).
- Per-field Base64 encoding in the G4 editor (5f90a3a).
- A calculator icon (a4ac73b).
- Unlimited license token information in the README (26bca9d).
- A recorded Android `InvokeLongClick` becomes an `InvokeLongClick` rule with `{{$ --Duration:<ms>}}`, and a recorded
  `SwitchActivity` becomes a `SwitchActivity` rule with `{{$ --Package:<package> --Activity:<activity>}}`, the
  arguments the new plugins read.
- A Chromium middle click recorded as `InvokeMiddleClick` becomes an `InvokeMiddleClick` rule on the recorded element.
- The release pipeline runs the Hub unit tests and the designer tests on Windows, without the `LocalOnly` category,
  and publishes the Docker images, the standalone Hub and the release only after they pass.

### Changed

- The Hub uses `G4.Api` 2026.11.25.80, the engine packages 2026.11.25.113 and the `G4.Plugins.*` packages
  2026.11.25.178, which add the middle-click plugins, `InvokeScroll --Relative`, and the `InvokeLongClick` and
  `SwitchActivity` plugins that recorded rules now use.
- A push that changes only changelogs or workflow files no longer starts a release.
- `FindTool` returns the full tool model with its namespace (17a1356).
- Tool discovery handles `take` differently (c34cd5b).
- The G4 CLI argument generation and array parameter handling were improved (fd8d04a).
- `POST cache/sync` returns a result for each MCP server (6a0ec20).
- The standalone install now states the updated SDK requirement (7512207).
- The automation pipeline uses Selenium Chrome (4299f89).
- The editor, APIs and manifest handling were enhanced (06f12c3).
- Theme, run states and editor behavior were improved (c15f627).
- The switch field is an accessible checkbox toggle (8558f28).
- Custom string fields no longer use Base64 for some fields (94f8e6e).

### Fixed

- The API test collection expects 200 for a job without rules. The engine accepts such a job since it added Parallel
  containers, and it runs nothing, so the case that expected 400 failed the release.
- A recorded scroll becomes `InvokeScroll` with `{{$ --Left:<x> --Top:<y> --Relative}}`, which scrolls by the Chromium
  wheel deltas, or by 120 pixels per notch along the recorded direction when there are none. Before, the argument used
  `--direction` and `--times`, which `InvokeScroll` does not read. The plugin runs JavaScript, so the rule replays on
  web pages only.
- Recorded `SendUser32KeyboardKey` rules, discrete keys and shortcut chords, no longer carry `onElement`, because the
  plugin sends the key to the focused window.
- The designer removes a `Switch` parameter's flag, such as `--Base64`, from the rule argument when the switch is turned off; before, the flag stayed and the engine still read the switch as on.
- SignalR automation error payloads are handled correctly (98b7551).
- Designer start-up keeps working when a plugin type is missing (f3e721a).
- Toolbox steps are found by plugin key and the Display Name label is correct (bbb09a8).
- Non-inlined toolbox icons are drawn dark in light VS Code themes (682a5ef).
- `Regex.Replace` is case-insensitive and has a timeout (7943416).
- Form controls in the smart editor inherit the font size (7ac0e97).
- The embedded Hub API token was removed from the README (097dc96).
