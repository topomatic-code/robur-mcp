using System;
using System.Collections.Generic;
using System.Linq;
using Topomatic.Cad.View;
using Topomatic.ToolBridge;
using Topomatic.ToolBridge.DependencyInjection;
using Topomatic.ToolBridge.Infrastructure;
using Topomatic.ToolBridge.Infrastructure.Implementation;
using Topomatic.ToolBridge.Services;
using Topomatic.ToolBridge.Settings;
using Topomatic.ToolBridge.Settings.Implementation;

namespace ToolBridge.Tests.ApiCheck_v0_1_0
{
    internal static class IntegrationHost
    {
        internal const string DomainToolName = "current_domain_tool";

        internal static Tool[] Generate(ToolProvider provider) => provider.GetTools().ToArray();

        internal static IToolManager CreateManager(IEnumerable<Tool> tools, IObjectStorage storage, IToolBridgeLogger logger, Func<CadView> view)
        {
            var container = new Container();
            container.RegisterSingleton<IContainer>(() => container);
            container.RegisterSingleton<IToolCollector>(() => new LocalCollector(tools));
            container.RegisterSingleton<IObjectStorage>(() => storage);
            container.RegisterSingleton<IToolBridgeLogger>(() => logger);
            container.RegisterSingleton<ICadViewProvider>(() => new LocalViewProvider(view));
            container.RegisterType<IToolManager, ToolManager>();
            var manager = container.CreateInstance<IToolManager>();
            return manager;
        }

        internal static string[] GetToolNames(IToolManager manager) =>
            manager.GetTools().Select(tool => tool.Name).ToArray();

        internal static void Call(IToolManager manager, string name)
        {
            manager.CallTool(new Dictionary<string, object>
            {
                { "tool_name", name },
                { "arguments", new Dictionary<string, object>() }
            });
        }

        internal static string[] CreateConfigurationNames(IEnumerable<Tool> tools, bool includeDomain)
        {
            var container = new Container();
            container.RegisterSingleton<IToolCollector>(() => new LocalCollector(tools));
            container.RegisterSingleton<IToolConfigLoader>(() => new MemoryConfigLoader(includeDomain));
            container.RegisterType<IToolSettings, ToolSettings>();
            var settings = container.CreateInstance<IToolSettings>();
            return settings.ToolConfigs.Select(config => config.Name).ToArray();
        }

        private sealed class LocalCollector : IToolCollector
        {
            public LocalCollector(IEnumerable<Tool> tools)
            {
                Tools = tools.ToList().AsReadOnly();
            }

            public IList<Tool> Tools { get; }
        }

        private sealed class LocalViewProvider : ICadViewProvider
        {
            private readonly Func<CadView> m_GetView;

            public LocalViewProvider(Func<CadView> getView) { m_GetView = getView; }
            public CadView CadView => m_GetView();
        }

        private sealed class MemoryConfigLoader : IToolConfigLoader
        {
            private readonly bool m_IncludeDomain;

            public MemoryConfigLoader(bool includeDomain) { m_IncludeDomain = includeDomain; }

            public List<ToolConfig> Load()
            {
                var configs = new List<ToolConfig>();
                if (m_IncludeDomain)
                    configs.Add(new ToolConfig(DomainToolName, "Current", "API compatibility probe",
                        "{\"type\":\"object\"}", true, false, true));
                return configs;
            }

            public void Save(List<ToolConfig> toolConfigs)
            {
                throw new InvalidOperationException("Проверка загрузки конфигураций не должна вызывать сохранение.");
            }
        }
    }
}
