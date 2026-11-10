# Module 4: Drive a session

[⬅ Back to overview](README.md) · [⬅ Module 3](03-discover-tools.md)

Where you are: you can find tools. About 8 minutes.

A session is one open browser. The agent starts it, sends rules to it, can read back the rules it sent, and then closes it.

## What you'll do

- Start a session and keep its `driverSession` id
- Send one rule and a group of rules
- Read, clear and close what the session keeps

## Steps

1. Start a session with `g4.StartSession`.

   Purpose, quoted: "Starts a new G4 session using specified driver binaries, browser (platform) name, and headless option."

   | Argument | Type | Required | Meaning |
   | --- | --- | --- | --- |
   | `driver` | string | Yes | The name of the browser driver to use, for example `ChromeDriver` |
   | `driverBinaries` | string | Yes | The path to the driver executable, or the URL of a Selenium Grid endpoint |
   | `token` | string | Yes | The G4 authentication token that authorizes the session start |
   | `capabilities` | object | No | W3C capabilities applied to every session match (`alwaysMatch`), for example browser options such as headless mode |
   | `firstMatch` | array of objects | No | An ordered list of W3C capability objects. The driver picks the first set it can satisfy |

   ```json
   {
       "jsonrpc": "2.0",
       "id": 20,
       "method": "tools/call",
       "params": {
           "name": "g4.StartSession",
           "arguments": {
               "driver": "ChromeDriver",
               "driverBinaries": "<driver-binaries-or-grid-url>",
               "token": "<your-g4-token>"
           }
       }
   }
   ```

   The answer holds one value, `driverSession`. Keep it. Every later call needs it. The Hub keeps the session, and an empty rule buffer for it, in memory.

2. Send one rule with `g4.SendRule`.

   Purpose, quoted: "Sends a G4 rule for execution with the provided parameters."

   | Argument | Type | Required | Meaning |
   | --- | --- | --- | --- |
   | `driverSession` | string | Yes | The session id from `g4.StartSession` |
   | `intent` | object | Yes | What the rule is meant to accomplish. See [Module 3](03-discover-tools.md) |
   | `token` | string | Yes | The G4 authentication token |
   | `rule` | object | Yes | The rule to run: `toolName` (required), `toolParameters` and `toolProperties` |

   The rule object works like this:

   | Field | Meaning |
   | --- | --- |
   | `toolName` | The plugin name, bare or as `Namespace:Name`. `tool_name`, `name` and `tool` are accepted as well |
   | `toolParameters` | Values the Hub turns into a G4 expression such as `{{$ --Key:Value}}`. An array becomes one switch for each item |
   | `toolProperties` | The rule itself as JSON, for example `argument`. If it holds a non-empty `argument`, the Hub keeps it and does not build one from `toolParameters` |

   ```json
   {
       "jsonrpc": "2.0",
       "id": 21,
       "method": "tools/call",
       "params": {
           "name": "g4.SendRule",
           "arguments": {
               "driverSession": "<driver-session-id>",
               "intent": {
                   "agentIntent": "open a web page",
                   "userIntent": "go to the example site"
               },
               "token": "<your-g4-token>",
               "rule": {
                   "toolName": "OpenUrl",
                   "toolProperties": {
                       "argument": "http://example.com"
                   }
               }
           }
       }
   }
   ```

   The answer holds the `driverSession`, the rules sent so far in this session as `buffer`, and the engine's `response` for this rule. If the rule is `CloseBrowser`, the Hub also removes the session and its buffer.

3. Send several rules with `g4.SendRules`.

   Purpose, quoted: "Sends a collection of G4 rules for sequential execution within a single driver session."

   It takes the same arguments as `g4.SendRule`, except `rules` replaces `rule`. `rules` is an array that must not be empty, and each entry has the same fields as `rule`. The Hub runs them as one job, in order.

4. Read the buffer with `g4.GetBuffer`.

   Purpose, quoted: "Retrieves the buffered rule execution entries for an active G4 session."

   | Argument | Type | Required | Meaning |
   | --- | --- | --- | --- |
   | `sessionId` | string | Yes | The id of the active session whose buffered rules should be returned |

   For an unknown session the Hub raises `No buffer was found for sessionId '<id>'. The session may be invalid, expired, or not initialized.` That error type is not on the list of failures that become readable `isError` results ([Module 1](01-endpoint-and-transport.md)). The SDK logs it on the Hub and the client gets a generic tool failure without this text. Look in the Hub log for the message. TODO(verify: live): the exact text the client sees was not captured.

5. Clean up. `g4.RemoveBuffer` and `g4.RemoveSession` each take one required argument, `sessionId`, and answer `removed` (true or false) with the id:

   | Tool | Purpose, quoted |
   | --- | --- |
   | `g4.RemoveBuffer` | "Removes the rule execution buffer associated with a G4 session, releasing its resources." |
   | `g4.RemoveSession` | "Removes an active G4 session from the sessions registry, releasing its resources." |

   These two tools only clear the Hub's records. To close the browser itself, send a `CloseBrowser` rule.

## Check your work

- You started a session and have a `driverSession` id.
- A `g4.SendRule` call returned the engine response and a `buffer` that lists your rule.
- You can say how to close the browser and how to clear the Hub's records.

TODO(verify: live): the answers above come from reading the code. No Hub was running, so no answer was captured.

## Where the facts come from

| Fact | Source |
| --- | --- |
| Purpose and argument wording | `src\G4.Services.Domain.V4\Mcp\G4SystemTools.cs`, nested `Catalog` class |
| Required and optional arguments | `src\G4.Services.UnitTests\Mcp\SystemToolsEndpointTests.cs:19-41` |
| Rule fields and their `[Required]` flags | `src\G4.Services.Domain.V4\Mcp\SystemToolModels.cs:64-97` |
| Rule conversion, accepted name keys, `argument` handling | `src\G4.Services.Domain.V4\Repositories\ToolsRepository.cs:565-635` |
| Session start returns `driverSession` | `ToolsRepository.cs:1321-1357` |
| `SendRule` answer and `CloseBrowser` cleanup | `ToolsRepository.cs:1181-1235` |
| `SendRules` empty-array rule | `ToolsRepository.cs:1240-1260` |
| Buffer error text and the two remove tools | `ToolsRepository.cs:857-884, 1139-1177` |

Next: [Module 5: Record an app](05-record-an-app.md)
