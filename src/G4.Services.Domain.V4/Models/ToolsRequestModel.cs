using G4.Api;
using G4.Models;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace G4.Models
{
    public class ToolRequestModel
    {
        public ToolArgumentsModel Arguments { get; set; }

        public string Name { get; set; }

        public class ToolArgumentsModel
        {
            /// <summary>
            /// The name or type of driver to use (e.g., "ChromeDriver").
            /// </summary>
            public string Driver { get; set; }

            /// <summary>
            /// The path or URL to driver binaries required by the tool.
            /// </summary>
            public string DriverBinaries { get; set; }

            /// <summary>
            /// An existing driver session ID, allowing reuse of an open session.
            /// </summary>
            public string DriverSession { get; set; }

            /// <summary>
            /// Gets or sets the intent or purpose of the tool invocation as a JSON element.
            /// </summary>
            public JsonElement Intent { get; set; }

            /// <summary>
            /// The OpenAI API key used for authentication.
            /// </summary>
            public string OpenaiApiKey { get; set; }

            /// <summary>
            /// The OpenAI model identifier (e.g., gpt-4, gpt-5).
            /// </summary>
            public string OpenaiModel { get; set; }

            /// <summary>
            /// The OpenAI API base URI (default or custom endpoint).
            /// </summary>
            public string OpenaiUri { get; set; }

            /// <summary>
            /// A reference to the associated rule definition for this invocation.
            /// </summary>
            public G4RuleModelBase Rule { get; set; }

            /// <summary>
            /// The general-purpose authentication token (if provided).
            /// </summary>
            public string Token { get; set; }
        }
    }
}
