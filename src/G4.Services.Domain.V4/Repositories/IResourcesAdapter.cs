using G4.Cache;
using G4.Models;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Represents an adapter for managing resources within the G4 domain,
    /// providing access to caching mechanisms and SVG data storage.
    /// </summary>
    public interface IResourcesAdapter
    {
        /// <summary>
        /// The CacheManager instance that handles caching operations within the application.
        /// This is used to store and retrieve data that needs to be cached for quick access.
        /// </summary>
        CacheManager Cache { get; set; }

        /// <summary>
        /// The SvgCacheModel instance that holds cached SVG data, where each SVG file's
        /// content is stored with the file name (without extension) as the key.
        /// </summary>
        SvgCacheModel SvgCache { get; set; }
    }
}