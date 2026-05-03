using G4.Abstraction.Cli;
using G4.Api;
using G4.Services.Domain.V4.Clients;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Represents an adapter interface for integrating various G4-related services and repositories.
    /// </summary>
    public interface IG4Adapter
    {
        /// <summary>
        /// Repository for managing bot-related data and operations.
        /// Provides access to bot creation, retrieval, updates, and deletions.
        /// </summary>
        public IBotsRepository Bots { get; set; }

        /// <summary>
        /// Gets or sets the factory used to parse values to and from the G4 CLI format.
        /// </summary>
        /// <remarks>
        /// This property is initialized with a default <see cref="CliFactory"/> instance
        /// and is used to handle conversions between runtime values and their
        /// command-line representation in the G4 CLI format.
        /// </remarks>
        CliFactory CliFactory { get; set; }

        /// <summary>
        /// The G4 client instance used for interacting with the G4 platform.
        /// Facilitates communication with G4-related services, endpoints, and resources.
        /// </summary>
        G4Client Client { get; set; }

        /// <summary>
        /// Repository for managing copilot-related data and interactions.
        /// Allows handling copilot-related functionalities like training, interaction, and state management.
        /// </summary>
        IMcpRepository Mcp { get; set; }

        /// <summary>
        /// Client for interacting with OpenAI services.
        /// Provides methods for leveraging OpenAI's language models and other AI capabilities.
        /// </summary>
        IOpenAiClient OpenAi { get; set; }

        /// <summary>
        /// Repository for managing tool-related operations within the G4 platform.
        /// Handles access to tools, configurations, and associated data for automation and other tasks.
        /// </summary>
        IToolsRepository Tools { get; set; }
    }
}
