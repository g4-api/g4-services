using System.Collections.Generic;

namespace G4.Services.Domain.V4.Models
{
    /// <summary>
    /// Represents a response model containing a message and a parameter.
    /// </summary>
    public class ParameterResponseModel
    {
        /// <summary>
        /// Gets or sets the message associated with the parameter response.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the parameter as a dictionary of key-value pairs.
        /// </summary>
        public Dictionary<string, string> Parameter { get; set; } = [];
    }
}
