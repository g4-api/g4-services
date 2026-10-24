using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace G4.Services.Domain.V4.Mcp
{
    /// <summary>
    /// Supplies the optional locator-selection rules applied by the g4.ResolveLocator system tool.
    /// </summary>
    /// <remarks>
    /// Absent members leave the matching rule unconstrained, so nullable flags distinguish "not set" from false.
    /// </remarks>
    public sealed class LocatorConstraintsModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the locator strategies to avoid.
        /// </summary>
        [Description("Locator strategies to avoid. Helps prevent use of brittle or unstable locators.")]
        public string[] Forbid { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a disabled element is rejected.
        /// </summary>
        [Description("If true, the element must not be disabled to qualify as a valid locator.")]
        public bool? MustBeEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a hidden element is rejected.
        /// </summary>
        [Description("If true, the element must be visible on the page to qualify as a valid locator.")]
        public bool? MustBeVisible { get; set; }

        /// <summary>
        /// Gets or sets the preferred locator strategies in priority order.
        /// </summary>
        [Description("Preferred locator strategies, in priority order. The first matching strategy will be used.")]
        public string[] Prefer { get; set; }

        #endregion
    }

    /// <summary>
    /// Supplies the user and agent intent pair that system tools use for discovery, ranking, and execution context.
    /// </summary>
    /// <remarks>
    /// The members serialize to the same names as <see cref="Models.IntentModel"/>, which the repository reads.
    /// </remarks>
    public sealed class ToolIntentModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the normalized intent produced by the agent.
        /// </summary>
        [Required]
        [Description("The normalized intent produced by the agent for tool discovery, routing, or execution.")]
        public string AgentIntent { get; set; }

        /// <summary>
        /// Gets or sets the original intent as expressed by the user.
        /// </summary>
        [Required]
        [Description("The original intent as expressed by the user.")]
        public string UserIntent { get; set; }

        #endregion
    }

    /// <summary>
    /// Supplies one plugin rule that the g4.SendRule and g4.SendRules system tools convert into an executable rule.
    /// </summary>
    /// <remarks>
    /// Parameter and property values stay untyped because each plugin manifest defines its own value shapes.
    /// </remarks>
    public sealed class ToolRuleModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the tool (plugin) name that defines the rule.
        /// </summary>
        [Required]
        [Description("The unique identifier of the tool (plugin) that defines the rule to be executed.")]
        public string ToolName { get; set; }

        /// <summary>
        /// Gets or sets the parameters passed to the rule during execution.
        /// </summary>
        [Description("The parameters to be passed to the rule during execution.")]
        public Dictionary<string, object> ToolParameters { get; set; }

        /// <summary>
        /// Gets or sets the additional properties and configuration of the rule.
        /// </summary>
        [Description("Additional properties and configuration for the rule.")]
        public Dictionary<string, object> ToolProperties { get; set; }

        #endregion
    }
}
