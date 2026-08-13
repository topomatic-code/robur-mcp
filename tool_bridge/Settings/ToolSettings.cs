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
            m_ToolConfigs.Clear();
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
                    m_ToolConfigs.Add(toolConfig);
                }
            }
            var tools = ToolCollector.Tools;
            var unsavedTools = tools.Where(t => !m_ToolConfigs.Any(cfg => cfg.Name.Equals(t.Definition.Name)));
            foreach (var tool in unsavedTools)
            {
                var definition = tool.Definition;
                var annotations = definition.Annotations;
                m_ToolConfigs.Add(
                    new ToolConfig(
                        definition.Name,
                        definition.Domain,
                        definition.Description,
                        definition.InputSchema.ToString(),
                        annotations.ReadOnlyHint,
                        annotations.DestructiveHint,
                        annotations.IdempotentHint)
                    {
                        Enabled = definition.DefaultEnabled,
                        ApprovalScope = ToolApprovalScope.None
                    }
                );
            }
            m_ToolConfigs.Sort((a, b) =>
            {
                var domainCmp = a.Domain.CompareTo(b.Domain);
                if (domainCmp == 0)
                    return a.Name.CompareTo(b.Name);
                return domainCmp;
            });
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
