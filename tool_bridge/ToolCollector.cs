using System.Collections.Generic;
using Topomatic.ApplicationPlatform;

namespace Topomatic.ToolBridge
{
    internal static class ToolCollector
    {
        private static readonly List<Tool> m_Tools;

        static ToolCollector()
        {
            m_Tools = new List<Tool>();
            var toolProviders = new List<ToolProvider>();
            ApplicationHost.Current.Plugins.Broadcast("tool_request", new string[] { }, new object[] { toolProviders });
            foreach (var toolProvider in toolProviders)
            {
                m_Tools.AddRange(toolProvider.GetTools());
            }
        }

        public static IList<Tool> Tools => m_Tools.AsReadOnly();
    }
}
