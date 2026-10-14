using Topomatic.Glg.Model;

namespace Topomatic.ToolBridge.Services
{
    internal interface IGeologyProvider
    {
        (string name, GeologyModel glg) GetGeology(string pathId);
    }
}
