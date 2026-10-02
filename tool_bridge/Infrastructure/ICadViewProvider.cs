using Topomatic.Cad.View;

namespace Topomatic.ToolBridge.Infrastructure
{
    internal interface ICadViewProvider
    {
        CadView CadView { get; }
    }
}
