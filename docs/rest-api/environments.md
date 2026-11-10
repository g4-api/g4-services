# Environments API

Store named sets of parameters that automations read with the parameters placeholder. All routes start with `/api/v4/g4/` and need no token header. See [tokens](../install-and-run/04-tokens.md) for what the Hub checks.

Base route: `/api/v4/g4/environments`

Values are stored Base64-encoded by default. Send `encode=false` to store them as they are, and `decode=true` on a read to get them back decoded.

## Actions at a glance

| Verb | Route | Summary |
| --- | --- | --- |
| DELETE | `/api/v4/g4/environments` | Clear all environments |
| GET | `/api/v4/g4/environments` | Retrieve all environments with global parameters |
| GET | `/api/v4/g4/environments/{name}` | Retrieve details of an environment by name |
| GET | `/api/v4/g4/environments/{environment}/parameter/{name}` | Retrieve a parameter from the specified environment |
| DELETE | `/api/v4/g4/environments/{name}` | Delete an environment by name |
| DELETE | `/api/v4/g4/environments/{environment}/parameter/{name}` | Delete a parameter from the specified environment |
| PUT | `/api/v4/g4/environments/{name}` | Update an existing environment or create a new one |
| PUT | `/api/v4/g4/environments/parameter/{name}` | Create or update a system parameter |
| PUT | `/api/v4/g4/environments/{environment}/parameter/{name}` | Create or update a parameter in the specified environment |

## Actions

### DELETE /api/v4/g4/environments

| Item | Value |
| --- | --- |
| Swagger summary | "Clear all environments" |
| Request body | none |
| Responses | 204 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/environments"
```

### GET /api/v4/g4/environments

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieve all environments with global parameters" |
| Request body | none |
| Responses | 200 |

```bash
curl "http://localhost:9944/api/v4/g4/environments"
```

### GET /api/v4/g4/environments/{name}

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieve details of an environment by name" |
| Request body | none |
| Query | decode (optional, default false): Base64-decode the values. |
| Responses | 200, 404 |

```bash
curl "http://localhost:9944/api/v4/g4/environments/TempEnvironment?decode=true"
```

### GET /api/v4/g4/environments/{environment}/parameter/{name}

| Item | Value |
| --- | --- |
| Swagger summary | "Retrieve a parameter from the specified environment" |
| Request body | none |
| Query | decode (optional, default false): Base64-decode the value. |
| Responses | 200, 404 |

```bash
curl "http://localhost:9944/api/v4/g4/environments/DocumentationEnvironment/parameter/TempEnvironment?decode=true"
```

### DELETE /api/v4/g4/environments/{name}

| Item | Value |
| --- | --- |
| Swagger summary | "Delete an environment by name" |
| Request body | none |
| Responses | 204, 404 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/environments/TempEnvironment"
```

### DELETE /api/v4/g4/environments/{environment}/parameter/{name}

| Item | Value |
| --- | --- |
| Swagger summary | "Delete a parameter from the specified environment" |
| Request body | none |
| Responses | 204, 404 |

```bash
curl -X DELETE "http://localhost:9944/api/v4/g4/environments/DocumentationEnvironment/parameter/TempEnvironment"
```

### PUT /api/v4/g4/environments/{name}

| Item | Value |
| --- | --- |
| Swagger summary | "Update an existing environment or create a new one" |
| Request body | `Dictionary<string, string>` |
| Query | encode (optional, default true): Base64-encode the values before storing them. |
| Responses | 201, 204, 400 |

```bash
curl -X PUT "http://localhost:9944/api/v4/g4/environments/TempEnvironment?encode=false" \
    -H "Content-Type: application/json" \
    -d '{"Parameter1": "Foo Bar", "Parameter2": "1"}'
```

### PUT /api/v4/g4/environments/parameter/{name}

| Item | Value |
| --- | --- |
| Swagger summary | "Create or update a system parameter" |
| Request body | `string` |
| Query | encode (optional, default true): Base64-encode the value before storing it. |
| Responses | 200, 201, 400 |

```bash
curl -X PUT "http://localhost:9944/api/v4/g4/environments/parameter/TempEnvironment?encode=false" \
    -H "Content-Type: text/plain" \
    -d "ParameterValue"
```

### PUT /api/v4/g4/environments/{environment}/parameter/{name}

| Item | Value |
| --- | --- |
| Swagger summary | "Create or update a parameter in the specified environment" |
| Request body | `string` |
| Query | encode (optional, default true): Base64-encode the value before storing it. |
| Responses | 200, 201, 400 |

```bash
curl -X PUT "http://localhost:9944/api/v4/g4/environments/DocumentationEnvironment/parameter/TempEnvironment?encode=false" \
    -H "Content-Type: text/plain" \
    -d "ParameterValue"
```
