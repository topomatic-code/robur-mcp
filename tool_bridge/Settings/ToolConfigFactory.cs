using System.Collections.Generic;
using System.Linq;

namespace Topomatic.ToolBridge.Settings
{
    internal static class ToolConfigFactory
    {
        internal static List<ToolConfig> Create(IEnumerable<Tool> tools, IEnumerable<ToolConfig> savedConfigs)
        {
            var configs = new List<ToolConfig>(savedConfigs);
            var unsavedTools = tools.Where(t => !configs.Any(cfg => cfg.Name.Equals(t.Definition.Name)));
            foreach (var tool in unsavedTools)
            {
                var definition = tool.Definition;
                var annotations = definition.Annotations;
                configs.Add(
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
            configs.Sort((a, b) =>
            {
                var aName = !string.IsNullOrWhiteSpace(a.Domain) ? $"[{a.Domain}] {a.Name}" : a.Name;
                var bName = !string.IsNullOrWhiteSpace(b.Domain) ? $"[{b.Domain}] {b.Name}" : b.Name;
                return aName.CompareTo(bName);
            });
            return configs;
        }
    }
}
