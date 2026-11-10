# Module 3: Discover tools

[⬅ Back to overview](README.md) · [⬅ Module 2](02-register-a-client.md)

Where you are: your client is connected. About 6 minutes.

G4 has many plugins, but `tools/list` shows only 22 tools. The agent finds the plugins through four discovery tools. This module shows each one.

## What you'll do

- Learn the intent object that all discovery tools take
- Call the four discovery tools
- Pick the right one for the job

## Steps

1. Learn the intent object. Most discovery and execution tools take an `intent` with two required text fields:

   | Field | Required | Meaning |
   | --- | --- | --- |
   | `agentIntent` | Yes | The normalized intent produced by the agent for tool discovery, routing, or execution |
   | `userIntent` | Yes | The original intent as expressed by the user |

2. Start with `g4.ResolveTools`. It is the one-call choice.

   Purpose, quoted from the Hub: "Resolves the tools most relevant to the supplied intent in one call, returning each tool's complete model and input schema together with its examples ranked by the same intent."

   | Argument | Type | Required | Meaning |
   | --- | --- | --- | --- |
   | `intent` | object | Yes | The user and agent intent used to rank the most relevant tools and their examples |
   | `take` | integer | No | The maximum number of tools to return |
   | `examplesTake` | integer | No | The maximum number of examples to return for each tool |

   ```json
   {
       "jsonrpc": "2.0",
       "id": 2,
       "method": "tools/call",
       "params": {
           "name": "g4.ResolveTools",
           "arguments": {
               "intent": {
                   "agentIntent": "open a web page",
                   "userIntent": "go to the login page"
               },
               "take": 3,
               "examplesTake": 2
           }
       }
   }
   ```

3. Use `g4.FindTools` when you want a ranked list without examples.

   Purpose, quoted: "Retrieves the full list of available tools that the AI agent can invoke."

   | Argument | Type | Required | Meaning |
   | --- | --- | --- | --- |
   | `intent` | object | Yes | The user and agent intent used to discover and rank the most relevant tools for the current task |
   | `take` | integer | No | The maximum number of tools to return |

   ```json
   {
       "jsonrpc": "2.0",
       "id": 3,
       "method": "tools/call",
       "params": {
           "name": "g4.FindTools",
           "arguments": {
               "intent": {
                   "agentIntent": "click a button",
                   "userIntent": "click the login button"
               },
               "take": 3
           }
       }
   }
   ```

4. Use `g4.FindTool` when you already know the name.

   Purpose, quoted: "Retrieves the complete G4 tool model for an exact tool name or its qualified Namespace:Name, including its authoritative namespace and client tool schema."

   | Argument | Type | Required | Meaning |
   | --- | --- | --- | --- |
   | `toolName` | string | Yes | The name of the tool to find: its bare name, or its qualified `Namespace:Name` when several namespaces provide the same name |

5. Use `g4.FindExamples` to see how a tool is used.

   Purpose, quoted: "Searches the cached example library for rule examples that match the supplied tool name and intent, returning each example with its relevance score, exported rule properties, and parsed CLI parameters."

   | Argument | Type | Required | Meaning |
   | --- | --- | --- | --- |
   | `intent` | object | Yes | The user and agent intent used to search and rank matching examples |
   | `maxResults` | integer | Yes | The maximum number of examples to return in the search results |
   | `namespace` | string | Yes | A namespace used to scope the example search to a specific plugin group or category |
   | `toolName` | string | Yes | The unique identifier of the tool for which examples are being searched |
   | `threshold` | integer | No | The minimum relevance score required for an example to be included in the results |

6. Choose by need:

   | You want | Call |
   | --- | --- |
   | The right tools and their examples in one call | `g4.ResolveTools` |
   | A ranked list of candidate tools | `g4.FindTools` |
   | The full model of one known tool | `g4.FindTool` |
   | More examples of one tool | `g4.FindExamples` |

   If a name exists in more than one namespace, send the qualified `Namespace:Name`. The rule conversion passes an unknown or ambiguous name through unchanged, and the engine then rejects it with its own message and the list of candidates.

## Check your work

- You can write an `intent` object with both fields.
- You called `g4.ResolveTools` and found a plugin such as `OpenUrl` in the answer. TODO(verify: live): the answer was not captured because no Hub was running.
- You know when to use `g4.FindTool` instead.

## Where the facts come from

| Fact | Source |
| --- | --- |
| Tool names | `src\G4.Services.Domain.V4\Mcp\G4ToolNames.cs` |
| Purpose and argument wording | `src\G4.Services.Domain.V4\Mcp\G4SystemTools.cs`, nested `Catalog` class |
| Required and optional arguments | `src\G4.Services.UnitTests\Mcp\SystemToolsEndpointTests.cs:19-41` |
| Intent fields and `[Required]` | `src\G4.Services.Domain.V4\Mcp\SystemToolModels.cs:40-62` |
| Request body of `g4.FindTools` | `http\G4.Services.Mcp.http` (request 7) |
| Ambiguous names pass through to the engine | `src\G4.Services.Domain.V4\Repositories\ToolsRepository.cs:575-577` |

Next: [Module 4: Drive a session](04-drive-a-session.md)
