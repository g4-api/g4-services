namespace G4.Services.Domain.V4.Mcp
{
    /// <summary>
    /// Declares the published wire name of every built-in G4 MCP tool.
    /// </summary>
    /// <remarks>
    /// The attribute tool types and the tools repository both read these names, so renaming a tool is one change here
    /// instead of a silent mismatch between the MCP declaration and the repository dispatch. The names are a published
    /// contract: changing one changes what MCP clients call. Endpoint tests keep literal names on purpose so they
    /// detect such a change.
    /// </remarks>
    internal static class G4ToolNames
    {
        #region *** Constants    ***

        /// <summary>
        /// The repository-only tool that converts tool arguments into an executable rule without running it.
        /// </summary>
        public const string ConvertToRule = "g4.ConvertToRule";

        /// <summary>
        /// The system tool that searches the cached example library.
        /// </summary>
        public const string FindExamples = "g4.FindExamples";

        /// <summary>
        /// The system tool that returns one complete tool model by its exact name.
        /// </summary>
        public const string FindTool = "g4.FindTool";

        /// <summary>
        /// The system tool that ranks the available tools by intent.
        /// </summary>
        public const string FindTools = "g4.FindTools";

        /// <summary>
        /// The recording tool that returns the native automation of a stopped recording.
        /// </summary>
        public const string GetAutomation = "g4.GetAutomation";

        /// <summary>
        /// The system tool that returns the buffered rule execution entries of a session.
        /// </summary>
        public const string GetBuffer = "g4.GetBuffer";

        /// <summary>
        /// The repository-only tool that partitions the current application DOM into semantic segments.
        /// </summary>
        public const string GetDomSegments = "g4.GetDomSegments";

        /// <summary>
        /// The flow tool that returns the stored manifest of one flow.
        /// </summary>
        public const string GetFlow = "g4.GetFlow";

        /// <summary>
        /// The flow tool that lists the stored flows.
        /// </summary>
        public const string GetFlows = "g4.GetFlows";

        /// <summary>
        /// The recording tool that returns the state and diagnostics of a recording session.
        /// </summary>
        public const string GetRecordingStatus = "g4.GetRecordingStatus";

        /// <summary>
        /// The flow tool that runs a stored flow and waits for it to finish.
        /// </summary>
        public const string InvokeFlow = "g4.InvokeFlow";

        /// <summary>
        /// The system tool that registers a promoted flow-template capability.
        /// </summary>
        public const string RegisterCapability = "g4.RegisterCapability";

        /// <summary>
        /// The flow tool that stores a native automation as a flow.
        /// </summary>
        public const string RegisterFlow = "g4.RegisterFlow";

        /// <summary>
        /// The system tool that removes the rule execution buffer of a session.
        /// </summary>
        public const string RemoveBuffer = "g4.RemoveBuffer";

        /// <summary>
        /// The flow tool that removes one stored flow.
        /// </summary>
        public const string RemoveFlow = "g4.RemoveFlow";

        /// <summary>
        /// The system tool that removes a session from the sessions registry.
        /// </summary>
        public const string RemoveSession = "g4.RemoveSession";

        /// <summary>
        /// The system tool that resolves an element locator on the page.
        /// </summary>
        public const string ResolveLocator = "g4.ResolveLocator";

        /// <summary>
        /// The system tool that resolves the most relevant tools together with their ranked examples.
        /// </summary>
        public const string ResolveTools = "g4.ResolveTools";

        /// <summary>
        /// The system tool that executes one rule.
        /// </summary>
        /// <remarks>
        /// The tools repository has no dedicated branch for this name: it falls through to the rule execution path
        /// that also serves plugin tools.
        /// </remarks>
        public const string SendRule = "g4.SendRule";

        /// <summary>
        /// The system tool that executes an ordered collection of rules as one job.
        /// </summary>
        public const string SendRules = "g4.SendRules";

        /// <summary>
        /// The recording tool that starts the enabled recorders.
        /// </summary>
        public const string StartRecording = "g4.StartRecording";

        /// <summary>
        /// The system tool that starts a new browser automation session.
        /// </summary>
        public const string StartSession = "g4.StartSession";

        /// <summary>
        /// The flow tool that stops a running flow invocation.
        /// </summary>
        public const string StopFlowInvocation = "g4.StopFlowInvocation";

        /// <summary>
        /// The recording tool that stops a recording and returns its native automation.
        /// </summary>
        public const string StopRecording = "g4.StopRecording";

        #endregion
    }
}
