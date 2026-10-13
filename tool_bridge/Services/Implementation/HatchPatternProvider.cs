using System.Collections.Generic;
using System.Linq;
using Topomatic.Dwg;
using Topomatic.ToolBridge.Exceptions;

namespace Topomatic.ToolBridge.Services.Implementation
{
    internal sealed class HatchPatternProvider : IHatchPatternProvider
    {
        public IEnumerable<HatchPattern> GetDefinedPatterns()
        {
            var patternManager = HatchPatternManager.Current ??
                throw new PreconditionFailedException("Не удалось получить доступ к менеджеру штриховок.");
            return patternManager.GetDefinedPatterns() ?? Enumerable.Empty<HatchPattern>();
        }
    }
}
