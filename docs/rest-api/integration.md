# Integration API

Read the plugin cache, manifests and documents, and synchronize the cache with external repositories and MCP servers. All routes start with `/api/v4/g4/` and need no token header. See [tokens](../install-and-run/04-tokens.md) for what the Hub checks.

Base route: `/api/v4/g4/integration`

`ExternalRepository` in the examples is a `G4ExternalRepositoryModel`. Only `name` and `url` are required.

## Actions at a glance

| Verb | Route | Summary |
| --- | --- | --- |
| POST | `/api/v4/g4/integration/cache/find` | Finds MCP tools by intent |
| POST | `/api/v4/g4/integration/cache/find/examples` | Search cached tool examples by intent |
| GET | `/api/v4/g4/integration/cache` | Retrieves the current plugin cache. |
| POST | `/api/v4/g4/integration/cache` | Retrieves the plugin cache from specified external repositories. |
| POST | `/api/v4/g4/integration/cache/filter` | Retrieves the plugin cache filtered by toolboxes and tools. |
| GET | `/api/v4/g4/integration/cache/flows` | Retrieves the current flows cache. |
| GET | `/api/v4/g4/integration/cache/credentials` | Get all credentials |
| GET | `/api/v4/g4/integration/cache/credentials/{idOrName}` | Get a credential by id or name |
| GET | `/api/v4/g4/integration/cache/dataset` | Retrieves the plugin cache as a ZIP archive. |
| GET | `/api/v4/g4/integration/cache/sync` | Synchronizes the internal plugin cache. |
| POST | `/api/v4/g4/integration/cache/sync` | Synchronizes the plugin cache with external repositories and MCP servers. |
| GET | `/api/v4/g4/integration/documents/key/{key}` | Retrieves plugin Markdown documentation by key. |
| GET | `/api/v4/g4/integration/documents/type/{pluginType}/key/{key}` | Retrieves plugin Markdown documentation by plugin type and key. |
| POST | `/api/v4/g4/integration/documents/key/{key}` | Retrieves plugin Markdown documentation by key and repository. |
| POST | `/api/v4/g4/integration/documents/type/{pluginType}/key/{key}` | Retrieves plugin Markdown documentation by plugin type, key, and repository. |
| GET | `/api/v4/g4/integration/manifests/drivers` | Retrieves all registered drivers. |
| GET | `/api/v4/g4/integration/manifests/key/{key}` | Retrieves the plugin manifest by key. |
| GET | `/api/v4/g4/integration/manifests/type/{pluginType}/key/{key}` | Retrieves plugin manifest by plugin type and key. |
| POST | `/api/v4/g4/integration/manifests/key/{key}` | Retrieves plugin manifest by key and repository. |
| POST | `/api/v4/g4/integration/manifests/type/{pluginType}/key/{key}` | Retrieves plugin manifest by plugin type, key, and repository. |
| GET | `/api/v4/g4/integration/manifests` | Retrieves all plugin manifests. |
| POST | `/api/v4/g4/integration/manifests` | Retrieves plugin manifests from external repositories. |
| POST | `/api/v4/g4/integration/manifests/filter` | Retrieves the plugin manifests filtered by toolboxes and tools. |
| GET | `/api/v4/g4/integration/files` | Lists all static files in wwwroot. |
| GET | `/api/v4/g4/integration/svgs` | Lists all SVG files in wwwroot. |

## Actions

### POST /api/v4/g4/integration/cache/find

| Item | Value |
| --- | --- |
| Swagger summary | "Finds MCP tools by intent" |
| Request body | `FindToolsRequestModel` |
| Responses | 200, 400 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/integration/cache/find" \
    -H "Content-Type: application/json" \
    -d '{"intent": "click a button", "maxResults": 5}'
```

### POST /api/v4/g4/integration/cache/find/examples

| Item | Value |
| --- | --- |
| Swagger summary | "Search cached tool examples by intent" |
| Request body | `ExamplesQueryModel` |
| Responses | 200, 400 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/integration/cache/find/examples" \
    -H "Content-Type: application/json" \
    -d '{"intent": {"agentIntent": "click a button", "userIntent": "log in"}, "toolName": "InvokeClick"}'
```

### GET /api/v4/g4/integration/cache

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves the current plugin cache." |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/integration/cache"
```

### POST /api/v4/g4/integration/cache

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves the plugin cache from specified external repositories." |
| Request body | `G4ExternalRepositoryModel[]` |
| Responses | 200 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/integration/cache" \
    -H "Content-Type: application/json" \
    -d '[{"name": "ExternalRepository", "url": "http://localhost:9999"}]'
```

### POST /api/v4/g4/integration/cache/filter

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves the plugin cache filtered by toolboxes and tools." |
| Request body | `ToolboxesFilterRequestModel` |
| Responses | 200 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/integration/cache/filter" \
    -H "Content-Type: application/json" \
    -d '{"toolboxesFilter": {"toolboxes": [{"name": "UiControls", "enabled": false}], "tools": []}}'
```

### GET /api/v4/g4/integration/cache/flows

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves the current flows cache." |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/integration/cache/flows"
```

### GET /api/v4/g4/integration/cache/credentials

| Item | Value |
| --- | --- |
| Swagger summary | "Get all credentials" |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/integration/cache/credentials"
```

### GET /api/v4/g4/integration/cache/credentials/{idOrName}

| Item | Value |
| --- | --- |
| Swagger summary | "Get a credential by id or name" |
| Request body | none |
| Responses | 200, 404 |

```bash
curl "http://localhost:9944/api/v4/g4/integration/cache/credentials/my-credential"
```

### GET /api/v4/g4/integration/cache/dataset

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves the plugin cache as a ZIP archive." |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/integration/cache/dataset" -o plugin-cache.zip
```

### GET /api/v4/g4/integration/cache/sync

| Item | Value |
| --- | --- |
| Swagger summary | "Synchronizes the internal plugin cache." |
| Request body | none |
| Responses | 204 |

```bash
curl "http://localhost:9944/api/v4/g4/integration/cache/sync"
```

### POST /api/v4/g4/integration/cache/sync

| Item | Value |
| --- | --- |
| Swagger summary | "Synchronizes the plugin cache with external repositories and MCP servers." |
| Request body | `CacheSyncModel` |
| Responses | 200 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/integration/cache/sync" \
    -H "Content-Type: application/json" \
    -d '{"repositories": [{"name": "ExternalRepository", "url": "http://localhost:9999"}]}'
```

### GET /api/v4/g4/integration/documents/key/{key}

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves plugin Markdown documentation by key." |
| Request body | none |
| Responses | 200, 404 |

```bash
curl "http://localhost:9944/api/v4/g4/integration/documents/key/InvokeClick"
```

### GET /api/v4/g4/integration/documents/type/{pluginType}/key/{key}

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves plugin Markdown documentation by plugin type and key." |
| Request body | none |
| Responses | 200, 404 |

```bash
curl "http://localhost:9944/api/v4/g4/integration/documents/type/action/key/InvokeClick"
```

### POST /api/v4/g4/integration/documents/key/{key}

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves plugin Markdown documentation by key and repository." |
| Request body | `G4ExternalRepositoryModel` |
| Responses | 200, 404 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/integration/documents/key/InvokeClick" \
    -H "Content-Type: application/json" \
    -d '{"name": "ExternalRepository", "url": "http://localhost:9999"}'
```

### POST /api/v4/g4/integration/documents/type/{pluginType}/key/{key}

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves plugin Markdown documentation by plugin type, key, and repository." |
| Request body | `G4ExternalRepositoryModel` |
| Responses | 200, 404 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/integration/documents/type/action/key/InvokeClick" \
    -H "Content-Type: application/json" \
    -d '{"name": "ExternalRepository", "url": "http://localhost:9999"}'
```

### GET /api/v4/g4/integration/manifests/drivers

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves all registered drivers." |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/integration/manifests/drivers"
```

### GET /api/v4/g4/integration/manifests/key/{key}

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves the plugin manifest by key." |
| Request body | none |
| Query | expandFields (optional): comma-separated list of fields to include in the response. |
| Responses | 200, 404 |

```bash
curl "http://localhost:9944/api/v4/g4/integration/manifests/key/InvokeClick"
```

### GET /api/v4/g4/integration/manifests/type/{pluginType}/key/{key}

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves plugin manifest by plugin type and key." |
| Request body | none |
| Query | expandFields (optional): comma-separated list of fields to include in the response. |
| Responses | 200, 404 |

```bash
curl "http://localhost:9944/api/v4/g4/integration/manifests/type/action/key/InvokeClick"
```

### POST /api/v4/g4/integration/manifests/key/{key}

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves plugin manifest by key and repository." |
| Request body | `G4ExternalRepositoryModel` |
| Query | expandFields (optional): comma-separated list of fields to include in the response. |
| Responses | 200, 404 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/integration/manifests/key/InvokeClick" \
    -H "Content-Type: application/json" \
    -d '{"name": "ExternalRepository", "url": "http://localhost:9999"}'
```

### POST /api/v4/g4/integration/manifests/type/{pluginType}/key/{key}

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves plugin manifest by plugin type, key, and repository." |
| Request body | `G4ExternalRepositoryModel` |
| Query | expandFields (optional): comma-separated list of fields to include in the response. |
| Responses | 200, 404 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/integration/manifests/type/action/key/InvokeClick" \
    -H "Content-Type: application/json" \
    -d '{"name": "ExternalRepository", "url": "http://localhost:9999"}'
```

### GET /api/v4/g4/integration/manifests

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves all plugin manifests." |
| Request body | none |
| Query | expandFields (optional): comma-separated list of fields to include in the response. |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/integration/manifests"
```

### POST /api/v4/g4/integration/manifests

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves plugin manifests from external repositories." |
| Request body | `G4ExternalRepositoryModel[]` |
| Query | expandFields (optional): comma-separated list of fields to include in the response. |
| Responses | 200 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/integration/manifests" \
    -H "Content-Type: application/json" \
    -d '[{"name": "ExternalRepository", "url": "http://localhost:9999"}]'
```

### POST /api/v4/g4/integration/manifests/filter

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieves the plugin manifests filtered by toolboxes and tools." |
| Request body | `ToolboxesFilterRequestModel` |
| Responses | 200 |

```bash
curl -X POST "http://localhost:9944/api/v4/g4/integration/manifests/filter" \
    -H "Content-Type: application/json" \
    -d '{"toolboxesFilter": {"toolboxes": [{"name": "UiControls", "enabled": false}], "tools": []}}'
```

### GET /api/v4/g4/integration/files

| Item | Value |
| --- | --- |
| Swagger summary | "Lists all static files in wwwroot." |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/integration/files"
```

### GET /api/v4/g4/integration/svgs

| Item | Value |
| --- | --- |
| Swagger summary | "Lists all SVG files in wwwroot." |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/integration/svgs"
```
