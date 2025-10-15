namespace G4.Models
{
    /// <summary>
    /// Describes a function call requested by the model, including the function name
    /// and its JSON-encoded arguments.
    /// </summary>
    public class OpenAiFunctionCall
    {
        /// <summary>
        /// JSON string containing the arguments for the function call.
        /// This payload is passed to the target function for execution.
        /// </summary>
        public string Arguments { get; set; }

        /// <summary>
        /// The identifier of the function to invoke.
        /// Matches one of the registered function names in the client application.
        /// </summary>
        public string Name { get; set; }
    }
}
