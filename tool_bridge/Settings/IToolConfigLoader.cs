using System.Collections.Generic;

namespace Topomatic.ToolBridge.Settings
{
    internal interface IToolConfigLoader
    {
        List<ToolConfig> Load();
        void Save(List<ToolConfig> toolConfigs);
    }
}
