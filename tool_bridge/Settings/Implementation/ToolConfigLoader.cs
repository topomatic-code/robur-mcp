using System.Collections.Generic;
using System.IO;
using Topomatic.ApplicationEnvironment;
using Topomatic.Stg;

namespace Topomatic.ToolBridge.Settings.Implementation
{
    internal sealed class ToolConfigLoader : IToolConfigLoader
    {
        private const string SETTINGS_PATH = @"%UserAppDataPath%\Support\robur-mcp\settings\tool_settings.rbobj";

        private static readonly object m_SyncRoot = new object();

        public List<ToolConfig> Load()
        {
            lock (m_SyncRoot)
            {
                var settingsPath = ProcessEnvironment.Current.ExpandEnvironmentVariables(SETTINGS_PATH);
                var toolConfigs = new List<ToolConfig>();
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
                        toolConfigs.Add(toolConfig);
                    }
                }
                return toolConfigs;
            }
        }

        public void Save(List<ToolConfig> toolConfigs)
        {
            lock (m_SyncRoot)
            {
                var settingsPath = ProcessEnvironment.Current.ExpandEnvironmentVariables(SETTINGS_PATH);
                var dirName = Path.GetDirectoryName(settingsPath);
                Directory.CreateDirectory(dirName);
                var stgDocument = new StgDocument();
                var root = stgDocument.Body;
                var toolConfigsArr = root.AddArray("ToolConfigs", StgType.Node);
                foreach (var toolConfig in toolConfigs)
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
}
