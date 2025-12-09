using System.Collections.Generic;

namespace G4.Services.Domain.V4.Models
{
    /// <summary>
    /// Represents a cache for storing SVG data with their corresponding names as keys.
    /// This model allows quick access to SVG content by associating each SVG's name
    /// (without extension) to its raw SVG markup.
    /// </summary>
    public class SvgCacheModel
    {
        /// <summary>
        /// A dictionary that holds the SVG data.
        /// The key is the SVG file name (without the file extension),
        /// and the value is the SVG content as a string.
        /// </summary>
        public Dictionary<string, string> Svgs { get; set; } = [];
    }
}
