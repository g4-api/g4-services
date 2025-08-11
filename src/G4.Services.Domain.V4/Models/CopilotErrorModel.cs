using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace G4.Models
{
    public class CopilotErrorModel
    {
        public long Code { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
