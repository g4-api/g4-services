using G4.Services.Domain.V4.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Repositories
{
    public interface ICopilotRepository
    {
        object FindTool(string toolName, object id);

        object GetTools(object id);

        object Initialize(object id);

        object InvokeTool(JsonElement parameters, object id);
    }
}
