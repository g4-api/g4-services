using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace G4.Models
{
    public class CopilotInitializeResponseModel
    {
        public object Id { get; set; }
        public string Jsonrpc { get; set; }
        public ResultModel Result { get; set; }

        public class ResultModel
        {
            public string ProtocolVersion { get; set; }
            public Capabilities Capabilities { get; set; }
            public ServerInfo ServerInfo { get; set; }
        }

        public class Capabilities
        {
            public ToolsCapabilities Tools { get; set; }
        }

        public class ToolsCapabilities
        {
            public bool ListChanged { get; set; }
        }

        public class ServerInfo
        {
            public string Name { get; set; }
            public string Version { get; set; }
        }
    }
}
