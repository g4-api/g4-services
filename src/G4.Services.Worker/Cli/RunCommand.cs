using CommandBridge;

using System;
using System.Collections.Generic;

namespace G4.Services.Worker.Cli
{
    /// <summary>
    /// Represents the 'run' command responsible for setting various environment variables
    /// based on the provided command parameters. This command allows for dynamic configuration
    /// of environment settings, such as the hub address (HUB_URI) and others as needed.
    /// </summary>
    [Command(name: "run", description: "Executes the run command to set configuration environment variables.")]
    class RunCommand : CommandBase
    {
        // Dictionary mapping command names to their parameter metadata.
        // This metadata defines the available parameters for the 'run' command, allowing users
        // to configure various environment variables dynamically at runtime.
        private static readonly Dictionary<string, IDictionary<string, CommandData>> s_commands = new()
        {
            ["run"] = new Dictionary<string, CommandData>(StringComparer.Ordinal)
            {
                // Define the 'hub' parameter corresponding to the hub address.
                { "hub", new() { Name = "hubUri", Description = "Specifies the hub address to be set as the HUB_URI environment variable.", Mandatory = false } }
            }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="RunCommand"/> class using the defined command parameters.
        /// </summary>
        public RunCommand()
            : base(s_commands)
        { }

        /// <summary>
        /// Invokes the run command by processing the provided parameters and setting the corresponding
        /// environment variables. For example, if a valid hub URI is provided via the "hubUri" parameter,
        /// the HUB_URI environment variable is updated accordingly.
        /// </summary>
        /// <param name="parameters">A dictionary of command parameters, where each key is the parameter name and each value is the parameter value provided by the user.</param>
        protected override void OnInvoke(Dictionary<string, string> parameters)
        {
            // Attempt to retrieve the 'hubUri' parameter value from the provided parameters.
            var isHubUri = parameters.TryGetValue("hubUri", out var hubUri);

            // If the 'hubUri' parameter is not present or its value is empty, exit without making any changes.
            if (!isHubUri || string.IsNullOrEmpty(hubUri))
            {
                return;
            }

            // Set the HUB_URI environment variable to the value of the 'hubUri' parameter.
            Environment.SetEnvironmentVariable("HUB_URI", hubUri);
        }
    }
}
