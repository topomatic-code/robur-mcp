using Topomatic.Dwg;

namespace Topomatic.ToolBridge.Services
{
    internal interface IDrawingProvider
    {
        Drawing GetActiveDrawing(bool require);
    }
}
