using G4.Models.Schema;
using G4.Services.Domain.V4;
using G4.Services.Domain.V4.Models.Schema;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System.Linq;
using System.Net.Mime;

namespace G4.Services.Hub.Api.V4.Controllers
{
    [ApiController]
    [Route("/api/v4/g4/tools")]
    [ApiExplorerSettings(GroupName = "OpenAi Tools")]
    public class OpenAiToolsController(IDomain domain) : ControllerBase
    {
        private readonly IDomain _domain = domain;

        [HttpPost("find_tool")]
        #region *** OpenApi Documentation ***
        [Consumes("application/json")]
        [SwaggerOperation(
            summary: "Finds a tool by name or intent.",
            description: "Attempts to locate a tool in the registry. If the tool name is not provided or is inaccurate, the intent will be used as a fallback for vector-based lookup.",
            OperationId = "FindTool",
            Tags = ["AiTools"]
        )]
        [SwaggerResponse(
            statusCode: StatusCodes.Status200OK,
            description: "The tool was found successfully. Returns a ToolOutputSchema containing the matched tool details.",
            Type = typeof(ToolOutputSchema),
            ContentTypes = [MediaTypeNames.Application.Json]
        )]
        #endregion
        public IActionResult FindTool(
            [FromBody]
            [SwaggerRequestBody(
                description: "The input schema containing the tool name (optional) and/or intent (used for fallback lookup).",
                Required = true
            )]
            FindToolInputSchema schema)
        {
            // Attempt to locate the tool using the provided intent and/or tool name.
            var result = _domain.Tools.FindTool(schema.Intent, schema.ToolName);

            // Return the result wrapped in the standard output schema.
            return Ok(new ToolOutputSchema
            {
                Result = result
            });
        }

        [HttpPost("get_application_dom")]
        #region *** OpenApi Documentation ***
        [Consumes("application/json")]
        [SwaggerOperation(
            summary: "Retrieve the document model (DOM) of the current active session.",
            description: "Uses the G4 engine to extract the DOM of the specified driver session. " +
                "Authorization is performed using the provided token.",
            OperationId = "GetDocumentModel",
            Tags = ["AiTools"]
        )]
        [SwaggerResponse(
            statusCode: StatusCodes.Status200OK,
            description: "The document model (DOM) was retrieved successfully. " +
                "The result contains a dictionary representation of the application DOM.",
            Type = typeof(ToolOutputSchema),
            ContentTypes = [MediaTypeNames.Application.Json]
        )]
        #endregion
        public IActionResult GetDocumentModel(
            [FromBody]
            [SwaggerRequestBody(
                description: "Input schema containing the driver session identifier and authorization token required to retrieve the DOM.",
                Required = true
            )]
            GetDocumentModelInputSchema schema)
        {
            // Call into the domain service to retrieve the document model (DOM) 
            // for the specified driver session using the G4 engine.
            var result = _domain.Tools.GetDocumentModel(schema.DriverSession, schema.Token);

            // Return the result wrapped in an anonymous object for consistency with other API responses.
            return Ok(new ToolOutputSchema
            {
                Result = result
            });
        }

        [HttpGet("get_instructions")]
        #region *** OpenApi Documentation ***
        [SwaggerOperation(
            summary: "Retrieve instructions for a given policy.",
            description: "Returns the instruction set associated with the specified policy. " +
                "If no policy is provided, the 'default' policy instructions are returned.",
            OperationId = "GetInstructions",
            Tags = ["AiTools"]
        )]
        [SwaggerResponse(
            statusCode: StatusCodes.Status200OK,
            description: "The instructions for the requested (or default) policy were retrieved successfully.",
            Type = typeof(ToolOutputSchema),
            ContentTypes = [MediaTypeNames.Application.Json]
        )]
        #endregion
        public IActionResult GetInstructions(
            [FromQuery]
            [SwaggerParameter(
                description: "Optional policy name. If omitted or empty, the 'default' policy will be used.",
                Required = false
            )]
            string policy)
        {
            // Retrieve the instruction set for the requested policy (or "default" if none is provided).
            var result = _domain.Tools.GetInstructions(policy);

            // Return the instructions wrapped in a consistent response object.
            return Ok(new ToolOutputSchema
            {
                Result = result
            });
        }

        [HttpPost("resolve_locator")]
        #region *** OpenApi Documentation ***
        [Consumes("application/json")]
        [SwaggerOperation(
            summary: "Resolve a locator expression for a given driver session and intent.",
            description: "Uses the G4 engine to resolve a locator expression that can be used to identify " +
                         "DOM elements in the current active session. If the tool name is not available, " +
                         "the intent can be leveraged for semantic or vector-based lookup.",
            OperationId = "ResolveLocator",
            Tags = ["AiTools"]
        )]
        [SwaggerResponse(
            statusCode: StatusCodes.Status200OK,
            description: "The locator expression was successfully resolved and returned in the response.",
            Type = typeof(ToolOutputSchema),
            ContentTypes = [MediaTypeNames.Application.Json]
        )]
        #endregion
        public IActionResult ResolveLocator(
            [FromBody]
            [SwaggerRequestBody(
                description: "The input schema containing the driver session, intent, and authorization token " +
                    "required for resolving a locator expression.",
                Required = true
            )]
            ResolveLocatorInputSchema schema)
        {
            // Call into the domain service to resolve the locator
            // for the specified driver session and intent using the G4 engine.
            var result = _domain.Tools.ResolveLocator(schema);

            // Return the result wrapped in a consistent response object.
            return Ok(new ToolOutputSchema
            {
                Result = result
            });
        }

        [HttpPost("get_tools")]
        #region *** OpenApi Documentation ***
        [Consumes("application/json")]
        [SwaggerOperation(
            summary: "Retrieve a collection of available tools.",
            description: "Returns a dictionary of available tools, optionally filtered by intent (purpose) and/or tool types. " +
                         "If no filters are provided, all tools are returned.",
            OperationId = "GetTools",
            Tags = ["AiTools"]
        )]
        [SwaggerResponse(
            statusCode: StatusCodes.Status200OK,
            description: "The available tools were retrieved successfully. The response contains a dictionary mapping tool names to their definitions.",
            Type = typeof(ToolOutputSchema),
            ContentTypes = [MediaTypeNames.Application.Json]
        )]
        #endregion
        public IActionResult GetTools(
            [FromBody]
            [SwaggerRequestBody(
                description: "The input schema containing an optional intent (purpose) and/or tool types to filter the returned tools.",
                Required = false
            )]
            GetToolsInputSchema schema)
        {
            // Normalize the input schema to ensure it's not null.
            schema ??= new GetToolsInputSchema();

            // Retrieve the collection of available tools, applying any provided filters.
            var tools = _domain.Tools.GetTools(schema.Intent, schema.Types);

            // Format the result as a collection of tool names and descriptions.
            var result = (tools
                .Values
                .Where(i => i.Type != "system-tool")
                .Select(i => i.Metadata))
                .Concat(tools.Values.Where(i => i.Type == "system-tool").Cast<object>());

            // Return the tools wrapped in a consistent response schema.
            return Ok(new ToolOutputSchema
            {
                Result = result
            });
        }

        [HttpPost("start_g4_session")]
        #region *** OpenApi Documentation ***
        [Consumes("application/json")]
        [SwaggerOperation(
            summary: "Start a new G4 driver session.",
            description: "Creates a new driver session using the G4 engine. Requires driver name, driver binaries, and an authorization token. " +
                "Optional session capabilities can be provided via AlwaysMatch or FirstMatch.",
            OperationId = "StartSession",
            Tags = ["AiTools"]
        )]
        [SwaggerResponse(
            statusCode: StatusCodes.Status200OK,
            description: "The driver session was started successfully. The response contains details of the newly created session.",
            Type = typeof(ToolOutputSchema),
            ContentTypes = [MediaTypeNames.Application.Json]
        )]
        #endregion
        public IActionResult StartSession(
            [FromBody]
            [SwaggerRequestBody(
                description: "The input schema containing driver session startup parameters. " +
                    "Driver, DriverBinaries, and Token are required; AlwaysMatch and FirstMatch are optional.",
                Required = true
            )]
            StartSessionInputSchema schema)
        {
            // Call into the domain service to start a new session using the provided schema.
            var result = _domain.Tools.StartSession(schema);

            // Return the session details wrapped in a consistent response schema.
            return Ok(new ToolOutputSchema
            {
                Result = result
            });
        }

        [HttpPost("start_g4_rule")]
        #region *** OpenApi Documentation ***
        [Consumes("application/json")]
        [SwaggerOperation(
            summary: "Start execution of a rule in an active driver session.",
            description: "Uses the G4 engine to execute a rule within the specified driver session. " +
                "Requires the session identifier, a rule definition, and an authorization token.",
            OperationId = "StartRule",
            Tags = ["AiTools"]
        )]
        [SwaggerResponse(
            statusCode: StatusCodes.Status200OK,
            description: "The rule was executed successfully. The response contains the result of the rule execution.",
            Type = typeof(ToolOutputSchema),
            ContentTypes = [MediaTypeNames.Application.Json]
        )]
        #endregion
        public IActionResult StartRule(
            [FromBody]
            [SwaggerRequestBody(
                description: "The input schema containing the driver session identifier, rule definition, and authorization token.",
                Required = true
            )]
            StartRuleInputSchema schema)
        {
            // Call into the domain service to execute the rule within the specified session.
            var result = _domain.Tools.StartRule(schema);

            // Return the result wrapped in a consistent response schema.
            return Ok(new ToolOutputSchema
            {
                Result = result
            });
        }
    }
}
