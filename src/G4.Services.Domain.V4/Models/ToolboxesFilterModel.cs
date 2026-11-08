namespace G4.Models
{
    /// <summary>
    /// Selects which toolboxes and tools are shown, as the designer toolbox and the filtered cache endpoints apply it.
    /// </summary>
    /// <remarks>
    /// A toolbox is a plugin category. A tool entry names a plugin by its qualified <c>Namespace:Key</c> name, which
    /// matches that plugin only, or by its bare key, which matches the key in every namespace. A tool is decided by
    /// the first rule that matches: its qualified tool entry, its bare tool entry, its toolbox entries, and otherwise
    /// it is enabled. When several entries name the same toolbox or tool, the last one wins, so a later source
    /// overrides an earlier one. An entry without a name or without an enabled value is ignored.
    /// </remarks>
    public class ToolboxesFilterModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the toolbox entries, each naming a plugin category.
        /// </summary>
        public FilterEntryModel[] Toolboxes { get; set; } = [];

        /// <summary>
        /// Gets or sets the tool entries, each naming a plugin by its qualified or bare name.
        /// </summary>
        public FilterEntryModel[] Tools { get; set; } = [];

        #endregion

        #region *** Nested Types ***

        /// <summary>
        /// Represents one toolbox or tool entry of a <see cref="ToolboxesFilterModel"/>.
        /// </summary>
        public class FilterEntryModel
        {
            #region *** Properties   ***

            /// <summary>
            /// Gets or sets a value indicating whether the named toolbox or tool is shown.
            /// </summary>
            /// <remarks>
            /// A missing value leaves the entry out, so an incomplete entry can never hide a tool by default.
            /// </remarks>
            public bool? Enabled { get; set; }

            /// <summary>
            /// Gets or sets the toolbox name or the qualified or bare tool name.
            /// </summary>
            public string Name { get; set; }

            #endregion
        }

        #endregion
    }
}
