using G4.Models;
using G4.Models.Schema;
using G4.Services.Domain.V4.Models.Schema;

using System.Collections.Generic;
using System.Text.Json;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Defines a contract for managing and interacting with MCP tools,
    /// including discovery, retrieval, invocation, and synchronization.
    /// </summary>
    public interface IToolsRepository
    {
        /// <summary>
        /// Finds a specific tool by its unique name.
        /// </summary>
        /// <param name="intent">The intent or purpose for which the tool is being sought.</param>
        /// <param name="toolName">The unique identifier or name of the tool to locate.</param>
        /// <returns>A <see cref="McpToolModel"/> representing the tool if found; otherwise, <c>null</c>.</returns>
        McpToolModel FindTool(string intent, string toolName);

        /// <summary>
        /// Retrieves the document model (DOM) of the active session using the G4 engine.  
        /// The document model is returned as a dictionary of key–value pairs representing
        /// the structure of the application’s DOM at the time of the request.
        /// </summary>
        /// <param name="driverSession">The identifier of the active driver/browser session whose DOM should be retrieved.</param>
        /// <param name="token">The authorization token required by the G4 engine to process the request.</param>
        /// <returns>A dictionary representing the application DOM, where keys and values are defined by the G4 engine. Returns an empty dictionary if the DOM could not be retrieved.</returns>
        IDictionary<string, object> GetDocumentModel(string driverSession, string token);

        /// <summary>
        /// Retrieves the instruction set associated with the specified policy using the G4 engine.  
        /// If no policy name is provided or the value is empty, the default policy instructions are returned.
        /// </summary>
        /// <param name="policy">The name of the policy for which to retrieve instructions. If <c>null</c> or empty, the "default" policy will be used.</param>
        /// <returns>An object representing the resolved instruction set for the specified (or default) policy. The structure of the returned object is defined by the G4 engine.</returns>
        object GetInstructions(string policy);

        /// <summary>
        /// Retrieves a collection of available tools, optionally filtered by intent and/or type(s).  
        /// If neither filter is applied, all tools are returned.
        /// </summary>
        /// <param name="intent">Optional intent filter. If provided, only tools relevant to the specified intent are returned. If <c>null</c> or empty, no intent-based filtering is applied.</param>
        /// <param name="types">Optional tool type filters. If provided, only tools matching one of the given types are returned. If none are provided, no type-based filtering is applied.</param>
        /// <returns>A dictionary mapping tool names (<see cref="string"/>) to their corresponding <see cref="McpToolModel"/> definitions. If both filters are omitted, all available tools are returned.</returns>
        IDictionary<string, McpToolModel> GetTools(string intent, params string[] types);

        /// <summary>
        /// Invokes a tool dynamically using its input parameters.
        /// </summary>
        /// <param name="parameters">A <see cref="JsonElement"/> containing the tool invocation parameters (e.g., arguments, configuration, or input data).</param>
        /// <returns>The result of the tool execution, as an <see cref="object"/>.</returns>
        object InvokeTool(JsonElement parameters);

        /// <summary>
        /// Resolves a locator expression for the given request schema using the G4 engine.  
        /// The resolved locator can be used to identify UI elements or DOM nodes in the active session.
        /// </summary>
        /// <param name="schema">The input schema containing the driver session identifier, intent describing the target element or action, and the authorization token required for the G4 engine to process the request.</param>
        /// <returns>A string representing the resolved locator expression that can be used to query the DOM. Returns <c>null</c> or an empty string if no locator could be resolved.</returns>
        object ResolveLocator(ResolveLocatorInputSchema schema);

        /// <summary>
        /// Starts execution of a rule within an active G4 driver session.
        /// The rule definition is provided in the input schema and executed 
        /// against the specified driver session.
        /// </summary>
        /// <param name="schema">The input schema that includes the driver session identifier, the rule definition to execute, and the authorization token required by the G4 engine.</param>
        /// <returns>An object representing the outcome of the rule execution. The exact structure of the result is defined by the G4 engine.</returns>
        object StartRule(StartRuleInputSchema schema);

        /// <summary>
        /// Starts a new driver session using the provided input schema.  
        /// The session is created through the G4 engine using the driver configuration
        /// and authorization token specified in the schema.
        /// </summary>
        /// <param name="schema">The input schema containing session startup parameters, including driver type, driver binaries, and authorization token.</param>
        /// <returns>An object representing the newly created session. The exact structure is defined by the G4 engine.</returns>
        object StartSession(StartSessionInputSchema schema);

        /// <summary>
        /// Synchronizes the tool repository with its source (e.g., database, file system, or registry),
        /// ensuring the repository reflects the latest tool definitions.
        /// </summary>
        void SyncTools();
    }
}
