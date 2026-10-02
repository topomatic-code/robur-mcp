using System.Collections.Generic;

namespace Topomatic.ToolBridge.Settings
{
    internal interface IToolSettings
    {
        IList<ToolConfig> ToolConfigs { get; }

        ToolConfig GetConfig(string toolName);
        void Save();
    }
}
