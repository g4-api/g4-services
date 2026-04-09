using System.ComponentModel.DataAnnotations;

namespace G4.Models
{
    /// <summary>
    /// Represents a request to search for MCP tools based on an intent string.
    /// </summary>
    public class FindToolsRequestModel
    {
        /// <summary>
        /// Gets or sets the textual intent describing the desired tool action or usage.
        /// </summary>
        [Required]
        public string Intent { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of tools to return in the search results.
        /// </summary>
        public int MaxResults { get; set; }

        /// <summary>
        /// Gets or sets the relevance threshold for the tool search.
        /// Only tools with a score above or equal to this value will be included in the results.
        /// </summary>
        public int Threshold { get; set; }
    }
}