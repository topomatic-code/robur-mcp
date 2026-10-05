using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Topomatic.ApplicationEnvironment;
using Topomatic.ApplicationPlatform;
using Topomatic.ApplicationPlatform.Core;
using Topomatic.ApplicationPlatform.Plugins;
using Topomatic.Cad.View;
using Topomatic.ToolBridge.Dialogs;
using Topomatic.ToolBridge.Dialogs.Wrappers;
using Topomatic.ToolBridge.Infrastructure;
using Topomatic.ToolBridge.Infrastructure.Implementation;
using Topomatic.ToolBridge.Services;
using Topomatic.ToolBridge.Services.Implementation;
using Topomatic.ToolBridge.Settings;
using Topomatic.ToolBridge.Settings.Implementation;
using Topomatic.ToolBridge.Tools;

namespace Topomatic.ToolBridge
{
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
    internal sealed class ToolBridgeModule : PluginInitializator
    {
        private const string SystemLogDirectoryPathTemplate = @"%UserAppDataPath%\Support\robur-mcp\logs";

        public DependencyInjection.IContainer Container { get; private set; }

        public override void Initialize(PluginFactory factory)
        {
            base.Initialize(factory);

            var container = new DependencyInjection.Container();
            container.RegisterSingleton<DependencyInjection.IContainer>(c => container);
            container.RegisterSingleton<IObjectStorage, ObjectStorage>();
            container.RegisterSingleton<IProjectManager, ProjectManager>();
            container.RegisterSingleton<IMcpServerBootstrap, McpServerBootstrap>();
            container.RegisterSingleton<IToolBridgeBootstrap, ToolBridgeBootstrap>();
            container.RegisterSingleton<IToolCollector, ToolCollector>();
            container.RegisterSingleton<IToolManager, ToolManager>();
            container.RegisterSingleton<IToolConfigLoader, ToolConfigLoader>();
            container.RegisterSingleton<IToolSettings, ToolSettings>();
            container.RegisterSingleton<IMcpSettings, McpSettings>();
            container.RegisterSingleton<IDrawingProvider, DrawingProvider>();

            container.RegisterSingleton<ICadViewProvider>(c => new CadViewProvider(() =>
                (CadView)ApplicationHost.Current.MainForm.Invoke((Func<CadView>)(() =>
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
                }))));

#pragma warning disable CS0618 // Type or member is obsolete
            container.RegisterSingleton<IToolBridgeLogger>(c => ToolBridgeLogger.Instance);
#pragma warning restore CS0618 // Type or member is obsolete

            container.RegisterType<IToolBridgePipeServer, ToolBridgePipeServer>();

            Container = container;
        }

        [cmd("tool_bridge_project_opened")]
        private void ProjectOpened()
        {
            var container = Container;
            if (container == null)
                return;

            var mcpServerBootstrap = container.GetSingleton<IMcpServerBootstrap>();
            var mcpSettings = container.GetSingleton<IMcpSettings>();
            if (!mcpServerBootstrap.ServerRunning && mcpSettings.AutoRun)
                McpRun();
        }

        [cmd("tool_bridge_project_closed")]
        private void ProjectClosed()
        {
            var container = Container;
            if (container == null)
                return;

            var sessionStorage = container.GetSingleton<IObjectStorage>();
            sessionStorage.Clear();
        }

        [cmd("tool_bridge_log")]
        private void EnableSystemLogging()
        {
            var container = Container;
            if (container == null)
                return;

            var logger = container.GetSingleton<IToolBridgeLogger>();
            try
            {
                var expandedDirectoryPath = ProcessEnvironment.Current.ExpandEnvironmentVariables(SystemLogDirectoryPathTemplate);
                var directoryPath = Path.GetFullPath(expandedDirectoryPath);
                logger.EnableSystemLogging(directoryPath);
                logger.PublicInfo("System logging enabled. Log directory: " + directoryPath);
            }
            catch (Exception ex)
            {
                logger.PublicError("Failed to enable the Tool Bridge system log: " + ex.Message);
            }
        }

        [cmd("tool_bridge_init")]
        private void ToolBridgeInit()
        {
            var container = Container;
            if (container == null)
                return;

            var toolBridgeBootstrap = container.GetSingleton<IToolBridgeBootstrap>();
            toolBridgeBootstrap.Initialize();
        }

        [cmd("tool_bridge_shutdown")]
        private void ToolBridgeShutdown()
        {
            var container = Container;
            if (container == null)
                return;

            var toolBridgeBootstrap = container.GetSingleton<IToolBridgeBootstrap>();
            toolBridgeBootstrap.Shutdown();
        }

        [cmd("mcp_server_run")]
        private void McpServerRun()
        {
            var container = Container;
            if (container == null)
                return;

            var mcpServerBootstrap = container.GetSingleton<IMcpServerBootstrap>();
            mcpServerBootstrap.Run();
        }

        [cmd("mcp_server_shutdown")]
        private void McpServerShutdown()
        {
            var container = Container;
            if (container == null)
                return;

            var mcpServerBootstrap = container.GetSingleton<IMcpServerBootstrap>();
            mcpServerBootstrap.Shutdown();
        }

        [cmd("mcp_run")]
        private void McpRun()
        {
            ToolBridgeInit();
            McpServerRun();
        }

        [cmd("mcp_control_panel")]
        private void McpControlPanel()
        {
            var container = Container;
            if (container == null)
                return;

            var toolBridgeBootstrap = container.GetSingleton<IToolBridgeBootstrap>();
            var mcpServerBootsrap = container.GetSingleton<IMcpServerBootstrap>();
            var mcpSettings = container.GetSingleton<IMcpSettings>();

            var runMcp = mcpServerBootsrap.ServerRunning;
            var settings = new McpSettingsWrapper(mcpSettings, runMcp);
            if (McpControlPanelDlg.Execute(settings, ref runMcp))
            {
                settings.SaveChanges();
                if (runMcp)
                {
                    if (!toolBridgeBootstrap.ServerRunning)
                        ToolBridgeInit();
                    if (!mcpServerBootsrap.ServerRunning)
                        McpServerRun();
                }
                else
                {
                    if (mcpServerBootsrap.ServerRunning)
                        McpServerShutdown();
                    if (toolBridgeBootstrap.ServerRunning)
                        ToolBridgeShutdown();
                }
            }
        }

        [cmd("mcp_tool_settings")]
        private void ToolSettings()
        {
            var container = Container;
            if (container == null)
                return;

            var toolSettings = new ToolSettingsWrapper(container.GetSingleton<IToolSettings>());
            if (ToolSettingsDlg.Execute(toolSettings))
                toolSettings.SaveChanges();
        }

        [cmd("generate_tools")]
        private void GenerateTools(object[] args)
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
