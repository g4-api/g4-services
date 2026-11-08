namespace G4.Models
{
    /// <summary>
    /// Represents the request body of the filtered cache and manifests endpoints.
    /// </summary>
    public class ToolboxesFilterRequestModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the toolboxes and tools filter; a missing filter returns every plugin.
        /// </summary>
        public ToolboxesFilterModel ToolboxesFilter { get; set; }

        #endregion
    }
}
