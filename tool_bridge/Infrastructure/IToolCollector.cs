using System.Collections.Generic;

namespace Topomatic.ToolBridge.Infrastructure
{
    internal interface IToolCollector
    {
        IList<Tool> Tools { get; }
    }
}
