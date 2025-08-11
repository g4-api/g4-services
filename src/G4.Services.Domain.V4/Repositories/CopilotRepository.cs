using G4.Api;
using G4.Cache;
using G4.Extensions;
using G4.Models;

using HtmlAgilityPack;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Repository for handling Copilot JSON-RPC operations:
    /// manages session state, available tools (both plugin-based and system tools),
    /// and dispatches initialization and invocation requests.
    /// </summary>
    public class CopilotRepository : ICopilotRepository
    {
        #region *** Constants    ***
        // The JSON-RPC protocol version used in all responses.
        private const string JsonRpcVersion = "2.0";
        #endregion

        #region *** Fields       ***
        // Tracks active browser or agent sessions by session ID.
        private static readonly ConcurrentDictionary<object, object> _sessions = [];

        // Registry of all available tools by name.
        private static readonly ConcurrentDictionary<string, McpToolModel> _tools
            = new(StringComparer.OrdinalIgnoreCase);

        // Cache manager providing access to plugin manifests.
        private readonly CacheManager _cache;

        // Client for external G4 API calls, if needed by certain tools.
        private readonly G4Client _client;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Constructs a new <see cref="CopilotRepository"/>, loading
        /// plugin-based "Action" tools from cache and merging with built-in system tools.
        /// </summary>
        /// <param name="cache">The <see cref="CacheManager"/> instance containing plugin caches.</param>
        /// <param name="client">The <see cref="G4Client"/> used for any external service interactions.</param>
        public CopilotRepository(CacheManager cache, G4Client client)
        {
            // Store injected dependencies
            _cache = cache;
            _client = client;

            // Extract all plugin manifests from the cache
            var actions = _cache
                .PluginsCache
                .Values
                .SelectMany(i => i.Values)
                .Where(i => i.Manifest.PluginType == "Action" && i.Manifest.Key != "NoAction")
                .Select(i => i.Manifest)
                .DistinctBy(manifest => manifest.Key);//.Take(0);

            // Convert each action manifest into an MCP tool model
            var tools = actions
                .Select(manifest => manifest.ConvertToTool())
                .ToList();

            // Retrieve built-in system tools (e.g., start browser)
            var systemTools = GetSystemTools();

            // Combine plugin-based tools with system tools and populate the registry
            foreach (var tool in tools.Concat(systemTools).ToList())
            {
                // Add or overwrite entry by tool name
                _tools[tool.Name] = tool;
            }
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public CopilotToolsResponseModel FindTool(string toolName, object id)
        {
            // Look up the tool by name in the internal registry
            var tool = _tools.GetValueOrDefault(toolName);

            // If not found, return a JSON-RPC error response
            if (tool == null)
            {
                return new CopilotToolsResponseModel
                {
                    Id = id,
                    Jsonrpc = JsonRpcVersion,
                    Error = new CopilotErrorModel
                    {
                        Code = -32601,
                        Message = $"Tool '{toolName}' not found."
                    }
                };
            }

            // If found, return the tool schema in the Result
            return new CopilotToolsResponseModel
            {
                Id = id,
                Jsonrpc = JsonRpcVersion,
                Result = new McpToolModel
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    InputSchema = tool.InputSchema,
                    OutputSchema = tool.OutputSchema
                }
            };
        }

        /// <inheritdoc />
        public CopilotToolsResponseModel GetTools(object id) => new()
        {
            Id = id,
            Jsonrpc = JsonRpcVersion,
            Result = new CopilotToolsResponseModel.ResultModel()
            {
                Tools = _tools.Values
            }
        };

        /// <inheritdoc />
        public CopilotInitializeResponseModel Initialize(object id) => new()
        {
            Id = id,
            Jsonrpc = JsonRpcVersion,
            Result = new()
            {
                Capabilities = new()
                {
                    Tools = new()
                    {
                        ListChanged = true
                    }
                },
                ProtocolVersion = "2025-03-26",
                ServerInfo = new()
                {
                    Name = "g4-engine-copilot-mcp",
                    Version = "1.0.0"
                }
            }
        };

        public object InvokeTool(JsonElement parameters, object id)
        {
            var toolName = parameters.GetProperty("name").GetString();
            var tool = _tools.GetValueOrDefault(toolName);
            var arguments = parameters.GetProperty("arguments");

            var result = tool switch
            {
                // Handle built-in system tools
                { Name: "get_application_dom" } => GetApplicationDom(arguments.GetProperty("driver_binaries").GetString()),
                { Name: "start_g4_session" } => StartG4Session(arguments.GetProperty("driver_binaries").GetString()),
                { Name: "find_tool" } => FindTool(arguments.GetProperty("tool_name").GetString(), id),
                { Name: "get_tools" } => GetTools(id),
                // Handle plugin-based tools (e.g., Action rules)
                _ => StartG4Rule(arguments.GetProperty("session").GetString(), ConvertToRule(toolName, arguments))
            };

            var a = new
            {
                jsonrpc = JsonRpcVersion,
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

        private object StartG4Session(string driverBinaries)
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

        private object GetApplicationDom(string driverBinaries)
        {
            var authentication = new AuthenticationModel
            {
                Token = "3xezq5Yc33laNOPNP8yCsK33vQcQZ87E/zyLNNscYNeqvKTHAm9C3wAEDQV7X9+fuuHPhafDNXbSFgsbKmCncCKm7DRE5A6JtFSd90DNujujbQ3vLG4/4uSVCR76Z6VguIDSvRZ/pJTHCzBc9NNI/eb5fLHcjXyYrilm9NC7VTD/HOlgGC5CL+oFhHoR8s10YuI9QpRioZbyDHysFumpAAv3/PG/p/QBKNoQpjtsUgMytrnqr3m1bgyXITG0u5AUR2VpZLCXQO6MxU7kwLwvdNGXUDfajBVT29KyjXUEWN9dK0R38XmgZFQ7orkKfN2z0x2SMfC5mvTDM6as+/kYWFAvpqOZDhZ95sgQWp/zGig="
            };
            var driverParameters = new Dictionary<string, object>
            {
                ["driver"] = $"Id({driverBinaries})"
            };
            var environmentsSettings = new EnvironmentsSettingsModel
            {
                ReturnEnvironment = true
            };
            var automation = new G4AutomationModel
            {
                Authentication = authentication,
                DriverParameters = driverParameters,
                Settings = new G4SettingsModel
                {
                    EnvironmentsSettings = environmentsSettings
                },
                Stages = new List<G4StageModel>
                {
                    new G4StageModel
                    {
                        Jobs = new List<G4JobModel>
                        {
                            new G4JobModel
                            {
                                Rules =
                                [
                                    new ActionRuleModel
                                    {
                                        PluginName = "RegisterParameter",
                                        Argument = "{{$ --Name:PageObjectOutput --Value:{{$New-ScriptResult --Src:function getDom(){return document.documentElement.outerHTML} return getDom();}} --Scope:Session}}"
                                    }
                                ]
                            }
                        }
                    }
                }
            };
            var response = _client.Automation.Invoke(automation);
            var session = response.Values.Last().Sessions.Last();
            _sessions[session.Key] = session.Value;

            var dom = $"{session.Value.Environment.SessionParameters.Get("PageObjectOutput", "<html></html>")}".ConvertFromBase64();
            var document = new HtmlDocument();
            document.LoadHtml(dom);

            document.DocumentNode.Clean();
            var a = document.DocumentNode.OuterHtml;

            return new
            {
                dom = document.DocumentNode.OuterHtml
            };
        }

        private object StartG4Rule(string driverBinaries, G4RuleModelBase rule)
        {
            var driverParameters = new Dictionary<string, object>
            {
                ["driver"] = $"Id({driverBinaries})"
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
                                Rules = [rule]
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
            return [.. typeof(SystemTools).GetProperties()
                .Where(i => i.GetCustomAttributes(typeof(SystemToolAttribute), false).Length != 0)
                .Select(i => i.GetValue(null) as McpToolModel)
                .Where(i => i != null)];
        }

        private static G4RuleModelBase ConvertToRule(string toolName, JsonElement parameters)
        {
            var pluginName = _tools.GetValueOrDefault(key: toolName)?.G4Name;

            var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var rule = JsonSerializer.Deserialize<ActionRuleModel>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            rule.PluginName = pluginName;

            return rule;
        }

        private static G4AutomationModel NewAutomation()
        {
            return new G4AutomationModel
            {
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

            //var response = _client.Automation.Invoke(automation);

            //var session = response.Values.Last().Sessions.Last();
            //_sessions[session.Key] = session.Value;

            //return new
            //{
            //    session = session.Key
            //};
        }

        private static G4AutomationModel NewAutomation(G4RuleModelBase[] rules)
        {
            return new G4AutomationModel
            {
                Stages =
                [
                    new G4StageModel
                    {
                        Jobs =
                        [
                            new()
                            {
                                Rules =rules
                            }
                        ]
                    }
                ]
            };

            //var response = _client.Automation.Invoke(automation);

            //var session = response.Values.Last().Sessions.Last();
            //_sessions[session.Key] = session.Value;

            //return new
            //{
            //    session = session.Key
            //};
        }
        #endregion

        #region *** Nested Types ***
        private static class SystemTools
        {
            [SystemTool]
            public static McpToolModel StartG4Session => new()
            {
                Name = "start_g4_session",
                Description = "Starts a new G4 session using specified driver binaries, browser (platform) name, and headless option.",
                InputSchema = new McpToolModel.ParameterSchemaModel
                {
                    Type = "object",
                    Properties = new()
                    {
                        ["driver_binaries"] = new McpToolModel.ScehmaPropertyModel
                        {
                            Type = ["string"],
                            Description = "Path to the browser driver executable or Selenium Grid endpoint URL."
                        }
                    },
                    Required = ["driver_binaries"]
                },
                OutputSchema = new McpToolModel.ParameterSchemaModel
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpToolModel.ScehmaPropertyModel>
                    {
                        ["session"] = new McpToolModel.ScehmaPropertyModel
                        {
                            Type = ["string"],
                            Description = "Unique identifier for the newly created browser session."
                        }
                    },
                    Required = ["session"]
                }
            };

            [SystemTool]
            public static McpToolModel FindTool => new()
            {
                Name = "find_tool",
                Description = "Retrieves the metadata and schema for a specific tool by its unique name.",
                InputSchema = new McpToolModel.ParameterSchemaModel
                {
                    Type = "object",
                    Properties = new()
                    {
                        ["tool_name"] = new McpToolModel.ScehmaPropertyModel
                        {
                            Type = ["string"],
                            Description = "The unique identifier of the tool to find."
                        }
                    },
                    Required = ["tool_name"]
                },
                OutputSchema = new McpToolModel.ParameterSchemaModel
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpToolModel.ScehmaPropertyModel>
                    {
                        ["tool"] = new McpToolModel.ScehmaPropertyModel
                        {
                            Type = ["object"],
                            Description = "The tool's metadata including name, description, input and output schemas."
                        }
                    },
                    Required = ["tool"]
                }
            };

            [SystemTool]
            public static McpToolModel GetApplicationDom => new()
            {
                Name = "get_application_dom",
                Description = "Retrieves the full HTML markup of the application's Document Object Model (DOM) for the current browser session. Useful for inspecting or analyzing the current state of the loaded web page.",
                InputSchema = new McpToolModel.ParameterSchemaModel
                {
                    Type = "object",
                    Properties = new()
                    {
                        ["driver_binaries"] = new McpToolModel.ScehmaPropertyModel
                        {
                            Type = ["string"],
                            Description = "Path to the browser driver executable or Selenium Grid endpoint URL, or existing session by using the session id."
                        }
                    },
                    Required = ["driver_binaries"]
                },
                OutputSchema = new McpToolModel.ParameterSchemaModel
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpToolModel.ScehmaPropertyModel>
                    {
                        ["dom"] = new McpToolModel.ScehmaPropertyModel
                        {
                            Type = ["string"],
                            Description = "A string containing the full HTML markup of the page’s Document Object Model."
                        }
                    },
                    Required = ["dom"]
                }
            };

            [SystemTool]
            public static McpToolModel GetTools => new()
            {
                Name = "get_tools",
                Description = "Retrieves the full list of available tools that the Copilot agent can invoke.",
                InputSchema = new McpToolModel.ParameterSchemaModel
                {
                    Type = "object",
                    Properties = new()
                    {
                        // No input parameters required
                    },
                    Required = []
                },
                OutputSchema = new McpToolModel.ParameterSchemaModel
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpToolModel.ScehmaPropertyModel>
                    {
                        ["tools"] = new McpToolModel.ScehmaPropertyModel
                        {
                            Type = ["array", "object"],
                            Description = "An array of tool objects, each containing name, description, input and output schemas."
                        }
                    },
                    Required = ["tools"]
                }
            };
        }
        #endregion

        #region *** Attributes   ***
        [AttributeUsage(AttributeTargets.Property)]
        private sealed class SystemToolAttribute : Attribute { }
        #endregion
    }
}
