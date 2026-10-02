using System;
using Topomatic.Cad.View;

namespace Topomatic.ToolBridge.Infrastructure.Implementation
{
    internal sealed class CadViewProvider : ICadViewProvider
    {
        private readonly Func<CadView> m_CadViewProvider;

        public CadViewProvider(Func<CadView> cadViewProvider)
        {
            m_CadViewProvider = cadViewProvider;
        }

        public CadView CadView => m_CadViewProvider?.Invoke() ?? null;
    }
}
