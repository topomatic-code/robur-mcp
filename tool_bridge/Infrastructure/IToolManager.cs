using System.Collections.Generic;
using Topomatic.Cad.View;

namespace Topomatic.ToolBridge.Infrastructure
{
    internal interface IToolManager
    {
        CadView CadView { get; }

        IList<ToolDefinition> GetTools();
        object CallTool(Dictionary<string, object> parameters);
    }
}
