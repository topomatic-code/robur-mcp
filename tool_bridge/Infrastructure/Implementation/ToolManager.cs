using System;
using System.Collections.Generic;
using System.Linq;
using Topomatic.ApplicationPlatform;
using Topomatic.Cad.View;
using Topomatic.ToolBridge.DependencyInjection;
using Topomatic.ToolBridge.Exceptions;
using Topomatic.ToolBridge.Services;

namespace Topomatic.ToolBridge.Infrastructure.Implementation
{
    internal sealed class ToolManager : IToolManager
    {
        private readonly IContainer m_Container;
        private readonly IToolCollector m_ToolCollector;
        private readonly ICadViewProvider m_CadViewProvider;
        private readonly List<Tool> m_Tools;

        private bool m_Initialized;

        public ToolManager([Singleton] IContainer container)
        {
            m_Container = container;
            m_ToolCollector = container.GetSingleton<IToolCollector>();
            m_CadViewProvider = container.GetSingleton<ICadViewProvider>();
            m_Tools = new List<Tool>();
        }

        public CadView CadView => m_CadViewProvider.CadView;

        private void Initialize()
        {
            m_Tools.Clear();
            m_Tools.AddRange(m_ToolCollector.Tools);
            m_Initialized = true;
        }

        public IList<ToolDefinition> GetTools()
        {
            if (!m_Initialized)
                Initialize();

            return m_Tools.Select(t => t.Definition).ToList();
        }

        public object CallTool(Dictionary<string, object> parameters)
        {
            if (!m_Initialized)
                Initialize();

            if (parameters == null)
                throw new BadRequestException("params is required");

            var toolName = JsonUtils.RequireString(parameters, "tool_name");
            var args = JsonUtils.GetObject(parameters, "arguments", new Dictionary<string, object>());
            var tool = m_Tools.FirstOrDefault(t => t.Definition.Name == toolName);
            if (tool == null)
                throw new ToolNotFoundException(toolName);

            var func = tool.Func;
            if (func == null)
                throw new InvalidOperationException("Tool function is null: " + toolName);

            var cadView = CadView;
            if (cadView == null)
            {
                throw new PreconditionFailedException(
                    "Не удалось получить активный видовой экран. " +
                    "Активируйте необходимую модель в структуре проекта и перейдите на требуемый видовой экран."
                );
            }

            object result = null;
            cadView.Invoke((Action)(() =>
            {
                var provider = tool.Provider;
                if (provider != null)
                {
                    provider.AppHost = ApplicationHost.Current;
                    provider.CadView = cadView;
                    provider.Container = m_Container;

#pragma warning disable CS0618 // Type or member is obsolete
                    provider.SessionStorage = (ObjectStorage)m_Container.GetSingleton<IObjectStorage>();
                    provider.Logger = (ToolBridgeLogger)m_Container.GetSingleton<IToolBridgeLogger>();
#pragma warning restore CS0618 // Type or member is obsolete

                }
                result = func(args);
            }));

            return result;
        }
    }
}
