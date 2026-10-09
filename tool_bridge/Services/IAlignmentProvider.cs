using Topomatic.Alg;

namespace Topomatic.ToolBridge.Services
{
    internal interface IAlignmentProvider
    {
        (string name, Alignment alg) GetAlignment(string pathId);
    }
}
