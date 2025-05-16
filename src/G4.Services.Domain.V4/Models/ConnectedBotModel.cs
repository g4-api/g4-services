using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace G4.Models
{
    /// <summary>
    /// Represents a connected bot instance, including its identity, type, status, and timestamps.
    /// </summary>
    public class ConnectedBotModel
    {
        /// <summary>
        /// Gets or sets the UTC timestamp when the bot was first created.
        /// </summary>
        public DateTime CreatedOn { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the connection to the bot.
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// Gets or sets the host name or IP address used for callback communication with the bot.
        /// </summary>
        public string CallbackHost { get; set; }

        /// <summary>
        /// Gets or sets the callback ingress URI used by the background job's HTTP listener.
        /// </summary>
        [Url(ErrorMessage = "CallbackIngress must be a valid HTTP or HTTPS URL.")]
        [StringSyntax(StringSyntaxAttribute.Uri)]
        public string CallbackIngress { get; set; }

        /// <summary>
        /// Gets or sets the port number used for callback communication with the bot.
        /// </summary>
        public int CallbackPort { get; set; }

        /// <summary>
        /// Gets or sets the callback URI for the bot, used for communication.
        /// </summary>
        [Required]
        [Url(ErrorMessage = "CallbackUri must be a valid HTTP or HTTPS URL.")]
        [StringSyntax(StringSyntaxAttribute.Uri)]
        public string CallbackUri { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the bot.
        /// </summary>
        [Required]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the bot's metadata was last modified.
        /// </summary>
        public DateTime LastModifiedOn { get; set; }

        /// <summary>
        /// Gets or sets the machine (hostname or IP) where the bot is running.
        /// </summary>
        [Required]
        public string Machine { get; set; }

        /// <summary>
        /// Gets or sets the human‑readable name of the bot.
        /// </summary>
        [Required]
        [RegularExpression(
            pattern: "^[a-z0-9\\-]+$",
            ErrorMessage = "Value may only contain lowercase letters (a–z), digits (0–9) and hyphens (-).")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the operating system version of the machine where the bot is running.
        /// </summary>
        [Required]
        public string OsVersion { get; set; }

        /// <summary>
        /// Gets or sets the current operational status of the bot (e.g., "Ready", "Working").
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the type or category of the bot (e.g., "File Listener Bot", "Static Bot").
        /// </summary>
        [Required]
        public string Type { get; set; }
    }
}
