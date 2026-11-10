# Module 7: Resolve a locator

[⬅ Back to overview](README.md) · [⬅ Module 6](06-flows-as-tools.md)

Where you are: you can run sessions and flows. About 4 minutes.

`g4.ResolveLocator` asks an OpenAI model to pick a locator for an element on the page the session has open. It is the one tool that calls out to an AI service, so it needs OpenAI details that the other tools do not.

## What you'll do

- Learn the arguments of `g4.ResolveLocator`
- Pass the OpenAI key, model and address with the call
- Avoid the most common mistake: expecting the Hub settings to fill them

## Steps

1. Know what the tool does. Purpose, quoted: "Retrieves a locator for a specific element on the page based on the provided parameters." The Hub reads the page DOM of the session, sends it together with your intent to the model, and returns the JSON that the model answers with.

2. Call it with all of its required arguments:

   | Argument | Type | Required | Meaning |
   | --- | --- | --- | --- |
   | `driverSession` | string | Yes | The id of the active browser session in which the locator search is performed |
   | `intent` | object | Yes | The element to locate on the page, used to guide the locator strategy |
   | `openaiApiKey` | string | Yes | OpenAI authentication token for the locator retrieval request |
   | `openaiModel` | string | Yes | The OpenAI model identifier, for example `gpt-4o` or `gpt-4.1-mini` |
   | `openaiUri` | string | Yes | The OpenAI API endpoint URI for the request |
   | `token` | string | Yes | The G4 authentication token |
   | `constraints` | object | No | Rules for choosing the locator |

   The `constraints` object has four optional fields:

   | Field | Type | Meaning |
   | --- | --- | --- |
   | `forbid` | array of strings | Locator strategies to avoid, to prevent brittle or unstable locators |
   | `prefer` | array of strings | Preferred locator strategies, in priority order. The first matching strategy is used |
   | `mustBeEnabled` | boolean | If true, the element must not be disabled |
   | `mustBeVisible` | boolean | If true, the element must be visible on the page |

   ```json
   {
       "jsonrpc": "2.0",
       "id": 50,
       "method": "tools/call",
       "params": {
           "name": "g4.ResolveLocator",
           "arguments": {
               "driverSession": "<driver-session-id>",
               "intent": {
                   "agentIntent": "locate the login button",
                   "userIntent": "click the login button"
               },
               "openaiApiKey": "<your-openai-api-key>",
               "openaiModel": "gpt-4.1-mini",
               "openaiUri": "<your-openai-chat-completions-url>",
               "token": "<your-g4-token>",
               "constraints": {
                   "mustBeVisible": true
               }
           }
       }
   }
   ```

3. Do not rely on the Hub OpenAI settings. The settings `OpenAi:ApiKey` and the variables `OPENAI_API_KEY` and `OPENAI_API_ENDPOINT` are read by the Hub's own OpenAI client, which is a different feature. The locator code does not read them. It builds its request from the three `openai*` arguments only, and it posts to `openaiUri` exactly as you give it. If you leave `openaiUri` out, the call fails before any request is sent.

4. Know the request shape. The Hub sends a Chat Completions style body, with `model` and two `messages`, and a `Bearer` header made from `openaiApiKey`. The model must answer with JSON text in `choices[0].message.content`. A server that does not follow this shape makes the call fail.

5. Keep the key safe. The key travels in the tool arguments, so the agent and your MCP client logs see it. Use a key with a limited budget and never paste a real one into a document.

## Check your work

- You can list the six required arguments.
- You know that `OPENAI_API_KEY` on the Hub does not feed this tool.
- A call with a valid session, key, model and address returned a locator. TODO(verify: live): this was not tried, because no Hub, browser session or OpenAI key was available.

## Where the facts come from

| Fact | Source |
| --- | --- |
| Tool wording and argument text | `src\G4.Services.Domain.V4\Mcp\G4SystemTools.cs`, nested `Catalog` class |
| Six required arguments, optional `constraints` | `src\G4.Services.UnitTests\Mcp\SystemToolsEndpointTests.cs:19-41` |
| Constraint fields | `src\G4.Services.Domain.V4\Mcp\SystemToolModels.cs:15-38` |
| Request is built from the arguments, posted to `openaiUri`, bearer from `openaiApiKey` | `src\G4.Services.Domain.V4\Repositories\ToolsRepository.cs:974-1048, 1463-1471` |
| The Hub OpenAI settings belong to `OpenAiClient`, not to the locator code | `src\G4.Services.Domain.V4\Clients\OpenAiClient.cs:34`; no `AppSettings.OpenAi` reference in `ToolsRepository.cs` |
| Where `OPENAI_*` variables are read | `src\G4.Settings\AppSettings.cs:154-213` |

Next: [Module 8: Refresh the catalog](08-refresh-the-catalog.md)
