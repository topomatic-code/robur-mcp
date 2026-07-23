using System;
using System.Collections.Generic;
using System.Linq;
using Topomatic.ApplicationPlatform;
using Topomatic.Cad.View;
using Topomatic.ToolBridge.Services;

namespace Topomatic.ToolBridge
{
    internal sealed class ToolManager
    {
        private readonly ObjectStorage m_SessionStorage;
        private readonly ToolBridgeLogger m_Logger;
        private readonly Func<CadView> m_CadViewProvider;
        private readonly List<Tool> m_Tools;

        public ToolManager(ObjectStorage sessionStorage, ToolBridgeLogger logger, Func<CadView> cadViewProvider)
        {
            m_SessionStorage = sessionStorage;
            m_Logger = logger;
            m_CadViewProvider = cadViewProvider;
            m_Tools = new List<Tool>();
        }

        public void Initialize()
        {
            m_Tools.Clear();
            var toolProviders = new List<ToolProvider>();
            ApplicationHost.Current.Plugins.Broadcast("tool_request", new string[] { }, new object[] { toolProviders });
            foreach (var toolProvider in toolProviders)
            {
                m_Tools.AddRange(toolProvider.GetTools());
            }
        }

        public IList<ToolDefinition> GetTools() => m_Tools.Select(t => t.Definition).ToList();

        public object CallTool(Dictionary<string, object> parameters)
        {
            if (parameters == null)
                throw new ArgumentException("params is required");
            var toolName = JsonUtils.RequireString(parameters, "tool_name");
            var args = JsonUtils.GetObject(parameters, "arguments", new Dictionary<string, object>());
            var tool = m_Tools.FirstOrDefault(t => t.Definition.Name == toolName);
            if (tool != null)
            {
                var func = tool.Func;
                if (func != null)
                {
                    var cadView = m_CadViewProvider.Invoke();
                    if (cadView != null)
                    {
                        object result = null;
                        cadView.Invoke((Action)(() =>
                        {
                            var provider = tool.Provider;
                            if (provider != null)
                            {
                                provider.AppHost = ApplicationHost.Current;
                                provider.CadView = cadView;
                                provider.SessionStorage = m_SessionStorage;
                                provider.Logger = m_Logger;
                            }
                            result = func.Invoke(args);
                        }));
                        return result;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "Не удалось получить активный видовой экран. " +
                            "Активируйте необходимую модель в структуре проекта и перейдите на требуемый видовой экран."
                        );
                    }
                }
                else
                {
                    throw new InvalidOperationException("Tool function is null: " + toolName);
                }
            }
            else
            {
                throw new InvalidOperationException("Unknown tool: " + toolName);
            }
        }
    }
}
