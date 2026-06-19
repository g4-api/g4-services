namespace G4.Services.Domain.V4.Models
{
    /// <summary>
    /// Represents a file invocation request.
    /// </summary>
    public class FileInvokeModel
    {
        /// <summary>
        /// Gets or sets the full or relative path of the file to invoke.
        /// </summary>
        public string Path { get; set; }
    }
}
