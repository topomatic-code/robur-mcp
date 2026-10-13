using System.Collections.Generic;
using Topomatic.Dwg;

namespace Topomatic.ToolBridge.Services
{
    internal interface IHatchPatternProvider
    {
        IEnumerable<HatchPattern> GetDefinedPatterns();
    }
}
