using Topomatic.Alg;

namespace Topomatic.ToolBridge.Services
{
    internal interface IAlignmentProvider
    {
        (string name, Alignment alg) GetAlignment(string pathId);
        bool TryGetActiveSection(Alignment alg, out int activeSection);
        bool TrySetActiveSection(Alignment alg, int activeSection);
    }
}
