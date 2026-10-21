using System.Collections.Generic;
using Topomatic.ToolBridge.Services.Models;

namespace Topomatic.ToolBridge.Services
{
    internal interface IRoburLogger
    {
        List<LogEntry> GetLog();
    }
}
