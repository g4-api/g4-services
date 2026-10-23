using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace G4.Services.Domain.V4.Mcp
{
    /// <summary>
    /// Declares one flow parameter that callers can supply when they run the flow.
    /// </summary>
    /// <remarks>
    /// The g4.RegisterFlow tool converts each entry into a plugin parameter of the stored manifest, so a flow
    /// registered through MCP advertises the same parameters as one added through the REST manifest.
    /// </remarks>
    public sealed class FlowParameterModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the value applied when the caller supplies none.
        /// </summary>
        [Description("The value applied to the {{$ parameters.Name }} placeholders when the caller supplies none.")]
        public string Default { get; set; }

        /// <summary>
        /// Gets or sets the description shown to callers that discover the flow.
        /// </summary>
        [Description("What the parameter controls, shown to callers that discover the flow.")]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a caller is expected to supply the parameter.
        /// </summary>
        /// <remarks>Null keeps the manifest default, which marks the parameter optional.</remarks>
        [Description("True when callers are expected to supply the parameter. Defaults to false.")]
        public bool? Mandatory { get; set; }

        /// <summary>
        /// Gets or sets the parameter name referenced by the flow's placeholders.
        /// </summary>
        [Required]
        [Description("The parameter name referenced by {{$ parameters.Name }} placeholders in the automation.")]
        public string Name { get; set; }

        #endregion
    }
}
