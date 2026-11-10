# Credentials API

List, create and remove stored credentials, including OAuth credentials. All routes start with `/api/v4/g4/` and need no token header. See [tokens](../install-and-run/04-tokens.md) for what the Hub checks.

Base route: `/api/v4/g4/credentials`

The callback action is called by the OAuth provider, not by your own client.

## Actions at a glance

| Verb | Route | Summary |
| --- | --- | --- |
| GET | `/api/v4/g4/credentials` | Get all credentials |
| GET | `/api/v4/g4/credentials/{idOrName}` | Get a credential by id or name |
| POST | `/api/v4/g4/credentials/oauth/{provider}` | Create OAuth credentials for a provider |
| DELETE | `/api/v4/g4/credentials/{idOrName}` | Remove a credential by id or name |
| GET | `/api/v4/g4/credentials/callback` | Persist OAuth credentials after provider callback |

## Actions

### GET /api/v4/g4/credentials

| Item | Value |
| --- | --- |
| Swagger summary | "Get all credentials" |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/credentials"
```

### GET /api/v4/g4/credentials/{idOrName}

| Item | Value |
| --- | --- |
| Swagger summary | "Get a credential by id or name" |
| Request body | none |
| Responses | 200, 404 |

```bash
curl "http://localhost:9944/api/v4/g4/credentials/my-credential"
```

### POST /api/v4/g4/credentials/oauth/{provider}

| Item | Value |
| --- | --- |
| Swagger summary | "Create OAuth credentials for a provider" |
| Request body | `OAuthCredentialModel` |
| Responses | 200 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/credentials/oauth/google" \
    -H "Content-Type: application/json" \
    -d '{"name": "my-credential", "clientId": "<client-id>", "clientSecret": "<client-secret>", "redirectUrl": "http://localhost:9944/api/v4/g4/credentials/callback", "scope": "<scope>"}'
```

### DELETE /api/v4/g4/credentials/{idOrName}

| Item | Value |
| --- | --- |
| Swagger summary | "Remove a credential by id or name" |
| Request body | none |
| Responses | 200, 404 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/credentials/my-credential"
```

### GET /api/v4/g4/credentials/callback

| Item | Value |
| --- | --- |
| Swagger summary | "Persist OAuth credentials after provider callback" |
| Request body | none |
| Query | code (required) and state (optional): returned by the OAuth provider. |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/credentials/callback?code=authorization-code&state=opaque-state"
```
