using System.Collections.Generic;
using Topomatic.ApplicationPlatform;

namespace Topomatic.ToolBridge.Infrastructure.Implementation
{
    internal sealed class ToolCollector : IToolCollector
    {
        private readonly List<Tool> m_Tools;

        public ToolCollector()
        {
            m_Tools = new List<Tool>();
            var toolProviders = new List<ToolProvider>();
            ApplicationHost.Current.Plugins.Broadcast("tool_request", new string[] { }, new object[] { toolProviders });
            foreach (var toolProvider in toolProviders)
            {
                m_Tools.AddRange(toolProvider.GetTools());
            }
        }

        public IList<Tool> Tools => m_Tools.AsReadOnly();
    }
}
