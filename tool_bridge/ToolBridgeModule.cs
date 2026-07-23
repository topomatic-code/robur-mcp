using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Topomatic.ApplicationPlatform;
using Topomatic.ApplicationPlatform.Core;
using Topomatic.ApplicationPlatform.Plugins;
using Topomatic.Cad.View;
using Topomatic.ToolBridge.Tools;

namespace Topomatic.ToolBridge
{
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
    internal sealed class ToolBridgeModule : PluginInitializator
    {
        public ToolBridgeModule()
        {

        }

        public override void Initialize(PluginFactory factory)
        {
            base.Initialize(factory);
        }

        [cmd("tool_bridge_init")]
        private void ToolBridgeInit()
        {
            ToolBridgeBootstrap.Instance.Initialize(() =>
            {
                return (CadView)ApplicationHost.Current.MainForm.Invoke((Func<CadView>)(() =>
                {
                    var cadView = CadView;
                    if (cadView == null)
                    {
                        var activeProject = ApplicationHost.Current.ActiveProject as ModelProject;
                        if (activeProject != null)
                        {
                            var windows = activeProject.GetWindows();
                            foreach (var window in windows)
                            {
                                if (window.Text == "План")
                                {
                                    window.Activate();
                                    cadView = CadView;
                                    break;
                                }
                            }
                        }
                    }
                    return cadView;
                }));
            });
        }

        [cmd("tool_bridge_shutdown")]
        private void ToolBridgeShutdown()
        {
            ToolBridgeBootstrap.Instance.Shutdown();
        }

        [cmd("mcp_server_run")]
        private void McpServerRun()
        {
            McpServerBootstrap.Instance.Run();
        }

        [cmd("mcp_server_shutdown")]
        private void McpServerShutdown()
        {
            McpServerBootstrap.Instance.Shutdown();
        }

        [cmd("mcp_run")]
        private void McpRun()
        {
            ToolBridgeInit();
            McpServerRun();
        }

        [cmd("generate_tools")]
        private void CulvertSettings(object[] args)
        {
            var toolProviders = args[0] as List<ToolProvider>;
            toolProviders.AddRange(
                new ToolProvider[]
                {
                    new ProjectTools(),
                    new CadViewTools(),
                    new DwgTools(),
                    new BlockTools(),
                    new CulvertTools(),
                    new LandscapingTools(),
                    new SolidTools(),
                    new TlcTools()
                }
            );
        }
    }
}
