using System.Collections.Generic;
using System.IO;
using System.Linq;
using Topomatic.ApplicationEnvironment;
using Topomatic.Stg;

namespace Topomatic.ToolBridge.Settings
{
    internal static class ToolSettings
    {
        private const string SETTINGS_PATH = @"%UserAppDataPath%\Support\robur-mcp\settings\tool_settings.rbobj";

        private static readonly List<ToolConfig> m_ToolConfigs;

        static ToolSettings()
        {
            m_ToolConfigs = new List<ToolConfig>();
            Load();
        }

        public static IList<ToolConfig> ToolConfigs => m_ToolConfigs.AsReadOnly();

        public static ToolConfig GetConfig(string toolName) => m_ToolConfigs.FirstOrDefault(cfg => cfg.Name.Equals(toolName));

        public static void Load()
        {
            var settingsPath = ProcessEnvironment.Current.ExpandEnvironmentVariables(SETTINGS_PATH);
            var savedConfigs = new List<ToolConfig>();
            if (File.Exists(settingsPath))
            {
                var stgDocument = new StgDocument();
                using (var stream = File.OpenRead(settingsPath))
                {
                    stgDocument.LoadFromStreamAsBinary(stream);
                }
                var root = stgDocument.Body;
                var toolConfigsArr = root.GetArray("ToolConfigs", StgType.Node);
                for (int i = 0; i < toolConfigsArr.Count; i++)
                {
                    var toolConfig = ToolConfig.LoadFromStg(toolConfigsArr.GetNode(i));
                    savedConfigs.Add(toolConfig);
                }
            }
            var configs = ToolConfigFactory.Create(ToolCollector.Tools, savedConfigs);
            m_ToolConfigs.Clear();
            m_ToolConfigs.AddRange(configs);
        }

        public static void Save()
        {
            var settingsPath = ProcessEnvironment.Current.ExpandEnvironmentVariables(SETTINGS_PATH);
            var dirName = Path.GetDirectoryName(settingsPath);
            Directory.CreateDirectory(dirName);
            var stgDocument = new StgDocument();
            var root = stgDocument.Body;
            var toolConfigsArr = root.AddArray("ToolConfigs", StgType.Node);
            foreach (var toolConfig in m_ToolConfigs)
            {
                toolConfig.SaveToStg(toolConfigsArr.AddNode());
            }
            using (var stream = File.OpenWrite(settingsPath))
            {
                stgDocument.SaveToStreamAsBinary(stream);
            }
        }
    }
}
