using System.Collections.Generic;
using System.Linq;
using Topomatic.ToolBridge.DependencyInjection;
using Topomatic.ToolBridge.Infrastructure;

namespace Topomatic.ToolBridge.Settings.Implementation
{
    internal sealed class ToolSettings : IToolSettings
    {
        private readonly IToolConfigLoader m_ToolConfigLoader;
        private readonly IToolCollector m_ToolCollector;
        private readonly List<ToolConfig> m_ToolConfigs;

        public ToolSettings([Singleton] IToolConfigLoader toolConfigLoader, [Singleton] IToolCollector toolCollector)
        {
            m_ToolConfigLoader = toolConfigLoader;
            m_ToolCollector = toolCollector;
            m_ToolConfigs = new List<ToolConfig>();
            Load();
        }

        public IList<ToolConfig> ToolConfigs => m_ToolConfigs.AsReadOnly();

        public ToolConfig GetConfig(string toolName) => m_ToolConfigs.FirstOrDefault(cfg => cfg.Name.Equals(toolName));

        private void Load()
        {
            m_ToolConfigs.Clear();
            m_ToolConfigs.AddRange(m_ToolConfigLoader.Load());

            var unsavedTools = m_ToolCollector.Tools.Where(t => !m_ToolConfigs.Any(cfg => cfg.Name.Equals(t.Definition.Name)));
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
                        annotations.IdempotentHint
                    )
                    {
                        Enabled = definition.DefaultEnabled,
                        ApprovalScope = ToolApprovalScope.None
                    }
                );
            }

            m_ToolConfigs.Sort((a, b) =>
            {
                var aName = !string.IsNullOrWhiteSpace(a.Domain) ? $"[{a.Domain}] {a.Name}" : a.Name;
                var bName = !string.IsNullOrWhiteSpace(b.Domain) ? $"[{b.Domain}] {b.Name}" : b.Name;
                return aName.CompareTo(bName);
            });
        }

        public void Save() => m_ToolConfigLoader.Save(m_ToolConfigs);
    }
}
