using System.Collections.Generic;
using System.Linq;
using Topomatic.ToolBridge.Settings;

namespace Topomatic.ToolBridge.Dialogs.Wrappers
{
    internal sealed class ToolSettingsWrapper
    {
        public ToolSettingsWrapper()
        {
            ToolConfigs = ToolSettings.ToolConfigs
                .Select(cfg => new ToolConfigWrapper(cfg))
                .ToList()
                .AsReadOnly();
        }

        public IList<ToolConfigWrapper> ToolConfigs { get; }

        public void SaveChanges()
        {
            foreach (var toolConfig in ToolConfigs)
            {
                toolConfig.SaveChanges();
            }
            ToolSettings.Save();
        }
    }
}
