# Changelog

All notable user-visible changes to the G4 Hub are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## Unreleased

### Added

- Recordings domain and an MCP server built on the official SDK (bc38142).
- MCP tool resolution (11a8d55).
- Namespace-aware tools and toolbox filtering (c65ecea).
- The `SendRules` tool runs a batch of rules in one call (6658bbf).
- The `RegisterCapability` tool (dfb1ad2).
- Framed, mutation-aware scrollbars in the smart editor (204ec5c).
- Per-field Base64 encoding in the G4 editor (5f90a3a).
- A calculator icon (a4ac73b).
- Unlimited license token information in the README (26bca9d).

### Changed

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

- SignalR automation error payloads are handled correctly (98b7551).
- Designer start-up keeps working when a plugin type is missing (f3e721a).
- Toolbox steps are found by plugin key and the Display Name label is correct (bbb09a8).
- Non-inlined toolbox icons are drawn dark in light VS Code themes (682a5ef).
- `Regex.Replace` is case-insensitive and has a timeout (7943416).
- Form controls in the smart editor inherit the font size (7ac0e97).
- The embedded Hub API token was removed from the README (097dc96).
