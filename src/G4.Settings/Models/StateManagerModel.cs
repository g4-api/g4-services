namespace G4.Models
{
    /// <summary>
    /// Represents the model for the state manager, which includes data encryption key.
    /// </summary>
    public class StateManagerModel
    {
        /// <summary>
        /// Gets or sets the base path used by the state manager.
        /// </summary>
        public string BasePath { get; set; }

        /// <summary>
        /// Gets or sets the data encryption key used by the state manager.
        /// </summary>
        public string DataEncryptionKey { get; set; }
    }
}
