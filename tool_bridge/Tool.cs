using System;
using System.Collections.Generic;

namespace Topomatic.ToolBridge
{
    internal sealed class Tool
    {
        public ToolProvider Provider { get; set; }
        public ToolDefinition Definition { get; set; }
        public Func<Dictionary<string, object>, object> Func { get; set; }
    }
}
