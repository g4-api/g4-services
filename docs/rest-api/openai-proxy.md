# OpenAI Proxy API

Forward chat completion requests to the configured OpenAI-compatible backend. All routes start with `/api/v4/g4/` and need no token header. See [tokens](../install-and-run/04-tokens.md) for what the Hub checks.

Base route: `/api/v4/g4/openaiproxy`

Route matching is case-insensitive, so `openaiproxy` and `OpenAiProxy` reach the same actions.

The two actions at `completions` and `chat/embeddings` are placeholders. They return 200 with an empty body.

## Actions at a glance

| Verb | Route | Summary |
| --- | --- | --- |
| GET | `/api/v4/g4/openaiproxy/models` | List available models |
| GET | `/api/v4/g4/openaiproxy` | Check proxy health status |
| POST | `/api/v4/g4/openaiproxy/chat/completions` | Generate chat completions |
| POST | `/api/v4/g4/openaiproxy/completions` | none |
| POST | `/api/v4/g4/openaiproxy/chat/embeddings` | none |

## Actions

### GET /api/v4/g4/openaiproxy/models

| Item | Value |
| --- | --- |
| Swagger summary | "List available models" |
| Request body | none |
| Responses | 200, 500 |

```bash
curl "http://localhost:9944/api/v4/g4/openaiproxy/models"
```

### GET /api/v4/g4/openaiproxy

| Item | Value |
| --- | --- |
| Swagger summary | "Check proxy health status" |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/openaiproxy"
```

### POST /api/v4/g4/openaiproxy/chat/completions

| Item | Value |
| --- | --- |
| Swagger summary | "Generate chat completions" |
| Request body | `OpenAiChatCompletionRequest` |
| Responses | 200, 204 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/openaiproxy/chat/completions" \
    -H "Content-Type: application/json" \
    -d '{"model": "<model-name>", "messages": [{"role": "user", "content": "Hello"}], "stream": false}'
```

### POST /api/v4/g4/openaiproxy/completions

| Item | Value |
| --- | --- |
| Swagger summary | "" (empty in the code) |
| Request body | none |
| Responses | `200` with an empty body. The action is a stub: it accepts any request and does not call the backend. |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/openaiproxy/completions"
```

### POST /api/v4/g4/openaiproxy/chat/embeddings

| Item | Value |
| --- | --- |
| Swagger summary | "" (empty in the code) |
| Request body | none |
| Responses | `200` with an empty body. The action is a stub: it accepts any request and does not call the backend. |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/openaiproxy/chat/embeddings"
```
