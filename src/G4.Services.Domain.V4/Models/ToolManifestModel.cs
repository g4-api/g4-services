using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Models
{
    public class ToolManifestModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
        public string Version { get; set; }
    }
}
