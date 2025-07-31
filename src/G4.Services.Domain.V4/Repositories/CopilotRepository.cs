using G4.Api;
using G4.Cache;
using G4.Extensions;
using G4.Models;
using G4.Services.Domain.V4.Models;

using Microsoft.Extensions.Hosting;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using static G4.Models.McpToolModel;

namespace G4.Services.Domain.V4.Repositories
{
    public class CopilotRepository : ICopilotRepository
    {
        private static readonly ConcurrentDictionary<string, object> _sessions = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, McpToolModel> _tools = new(StringComparer.OrdinalIgnoreCase);

        private readonly CacheManager _cache;
        private readonly G4Client _client;

        public CopilotRepository(CacheManager cache, G4Client client)
        {
            _cache = cache;
            _client = client;

            var actions = cache
                .PluginsCache
                .Values
                .SelectMany(i => i.Values)
                .Where(i => i.Manifest.PluginType == "Action")
                .Select(i => i.Manifest)
                .DistinctBy(i => i.Key).Take(0);

            var tools = actions.Select(i => i.ConvertToTool()).ToList();
            var systemTools = GetSystemTools();

            foreach (var tool in tools.Concat(systemTools).ToList())
            {
                // Register the tool in the cache
                _tools[tool.Name] = tool;
            }
        }

        public object FindTool(string toolName, object id)
        {
            // Only one tool is supported in this hard‑coded example
            if (toolName == "start_browser_session")
            {
                return new
                {
                    jsonrpc = "2.0",
                    id,
                    result = new
                    {
                        // Must match the tool name the agent discovered earlier
                        name = "start_browser_session",

                        // Human‑readable description shown in Copilot’s UI
                        description = "Starts a new browser session using specified driver binaries, browser name, and headless option.",

                        // JSON Schema for the parameters Copilot will prompt the user for
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                driverBinaries = new
                                {
                                    type = "string",
                                    description = "Path to the browser driver executable or Selenium Grid endpoint URL."
                                },
                                //browserName = new
                                //{
                                //    type = "string",
                                //    description = "Name of the browser to start (e.g., \"chrome\", \"firefox\")."
                                //},
                                //headless = new
                                //{
                                //    type = "boolean",
                                //    description = "Whether to run the browser in headless mode."
                            }
                        },
                        required = new[] { "driverBinaries" }
                    }
                };
            }


            // If the requested tool isn't registered, return a JSON-RPC "Method not found" error
            return new
            {
                jsonrpc = "2.0",
                id,
                error = new
                {
                    code = -32601,
                    message = $"Tool '{toolName}' not found."
                }
            };
        }

        public object GetTools(object id)
        {
            return new
            {
                jsonrpc = "2.0",
                id,
                result = new
                {
                    tools = _tools.Values.ToList(),
                }
            };
        }


        public object Initialize(object id)
        {
            return new
            {
                jsonrpc = "2.0",
                id,
                result = new
                {
                    protocolVersion = "2025-03-26",
                    capabilities = new { tools = new { listChanged = true } },
                    serverInfo = new { name = "MyMcpApi", version = "1.0.0" }
                }
            };
        }

        public object InvokeTool(JsonElement parameters, object id)
        {
            var toolName = parameters.GetProperty("name").GetString();
            var tool = _tools.GetValueOrDefault(toolName);

            var result = StartBrowserSession("http://localhost:4444/wd/hub");

            var a = new
            {
                jsonrpc = "2.0",
                id,
                result = new
                {
                    content = new[] {
                        new
                        {
                            type = "text",
                            text = JsonSerializer.Serialize(result)
                        }
                    },
                    structuredContent = result
                }
            };

            var jj = JsonSerializer.Serialize(a);

            return a;
        }

        private object StartBrowserSession(string driverBinaries)
        {
            var authentication = new AuthenticationModel
            {
                Token = string.Empty
            };

            var driverParameters = new Dictionary<string, object>
            {
                ["driver"] = "ChromeDriver",
                ["driverBinaries"] = driverBinaries
            };

            var automation = new G4AutomationModel
            {
                Authentication = new AuthenticationModel
                {
                    Token = "3xezq5Yc33laNOPNP8yCsK33vQcQZ87E/zyLNNscYNeqvKTHAm9C3wAEDQV7X9+fuuHPhafDNXbSFgsbKmCncCKm7DRE5A6JtFSd90DNujujbQ3vLG4/4uSVCR76Z6VguIDSvRZ/pJTHCzBc9NNI/eb5fLHcjXyYrilm9NC7VTD/HOlgGC5CL+oFhHoR8s10YuI9QpRioZbyDHysFumpAAv3/PG/p/QBKNoQpjtsUgMytrnqr3m1bgyXITG0u5AUR2VpZLCXQO6MxU7kwLwvdNGXUDfajBVT29KyjXUEWN9dK0R38XmgZFQ7orkKfN2z0x2SMfC5mvTDM6as+/kYWFAvpqOZDhZ95sgQWp/zGig="
                },
                DriverParameters = driverParameters,
                Stages = new List<G4StageModel>
                {
                    new G4StageModel
                    {
                        Jobs = new List<G4JobModel>
                        {
                            new G4JobModel
                            {
                            }
                        }
                    }
                }
            };

            var response = _client.Automation.Invoke(automation);

            var session = response.Values.Last().Sessions.Last();
            _sessions[session.Key] = session.Value;

            return new
            {
                session = session.Key
            };
        }

        private static McpToolModel[] GetSystemTools()
        {
            return new McpToolModel[]
            {
                new McpToolModel
                {
                    Name = "start_browser_session",
                    Description = "Starts a new browser session using specified driver binaries, browser name, and headless option.",
                    InputSchema = new McpToolModel.ParameterSchemaModel
                    {
                        Type = "object",
                        Properties = new()
                        {
                            ["driverBinaries"] = new ScehmaPropertyModel
                            {
                                Type = ["string"],
                                Description = "Path to the browser driver executable or Selenium Grid endpoint URL."
                            }
                        },
                        Required = ["driverBinaries"]
                    },
                    OutputSchema = new ParameterSchemaModel
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ScehmaPropertyModel>
                        {
                            ["session"] = new ScehmaPropertyModel
                            {
                                Type = ["string"],
                                Description = "Unique identifier for the newly created browser session."
                            }
                        },
                        Required = new[] { "session" }
                    }
                }
            };
        }
    }
}
