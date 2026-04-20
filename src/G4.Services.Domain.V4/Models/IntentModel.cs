namespace G4.Services.Domain.V4.Models
{
    /// <summary>
    /// Represents the resolved intent pair used by the agent pipeline.
    /// </summary>
    /// <remarks>This model separates the original user-facing intent from the normalized or rewritten intent produced by the agent for downstream processing.</remarks>
    public sealed class IntentModel
    {
        /// <summary>
        /// Gets or sets the normalized intent produced by the agent for tool
        /// discovery, routing, or execution.
        /// </summary>
        public string AgentIntent { get; set; }

        /// <summary>
        /// Gets or sets the original intent as expressed by the user.
        /// </summary>
        public string UserIntent { get; set; }
    }
}
