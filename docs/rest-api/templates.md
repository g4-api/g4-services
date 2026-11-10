# Templates API

Register, list and remove templates. All routes start with `/api/v4/g4/` and need no token header. See [tokens](../install-and-run/04-tokens.md) for what the Hub checks.

Base route: `/api/v4/g4/templates`

`template.json` in the example is a plugin manifest. See the [plugin manifest reference](../../../g4-engine/docs/plugin-manifest-reference.md).

## Actions at a glance

| Verb | Route | Summary |
| --- | --- | --- |
| PUT | `/api/v4/g4/templates` | Add or overwrite a template |
| DELETE | `/api/v4/g4/templates` | Clear all templates |
| GET | `/api/v4/g4/templates/{key}` | Retrieve a template manifest by key |
| GET | `/api/v4/g4/templates` | Retrieve all template manifests |
| DELETE | `/api/v4/g4/templates/{key}` | Remove a template by key |

## Actions

### PUT /api/v4/g4/templates

| Item | Value |
| --- | --- |
| Swagger summary | "Add or overwrite a template" |
| Request body | `G4PluginAttribute` |
| Responses | 204, 400, 409 |

```bash
curl -X PUT "http://localhost:9944/api/v4/g4/templates" \
    -H "Content-Type: application/json" \
    -d @template.json
```

### DELETE /api/v4/g4/templates

| Item | Value |
| --- | --- |
| Swagger summary | "Clear all templates" |
| Request body | none |
| Responses | 204 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/templates"
```

### GET /api/v4/g4/templates/{key}

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieve a template manifest by key" |
| Request body | none |
| Responses | 200, 404 |

```bash
curl "http://localhost:9944/api/v4/g4/templates/Login"
```

### GET /api/v4/g4/templates

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieve all template manifests" |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/templates"
```

### DELETE /api/v4/g4/templates/{key}

| Item | Value |
| --- | --- |
| Swagger summary | "Remove a template by key" |
| Request body | none |
| Responses | 204, 404 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/templates/Login"
```
