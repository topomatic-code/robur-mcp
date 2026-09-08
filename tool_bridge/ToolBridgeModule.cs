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
using Topomatic.ToolBridge.Services;
using Topomatic.ToolBridge.Settings;
using Topomatic.ToolBridge.Tools;

namespace Topomatic.ToolBridge
{
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
    internal sealed class ToolBridgeModule : PluginInitializator
    {
        private const string SystemLogDirectoryPathTemplate = @"%UserAppDataPath%\Support\robur-mcp\logs";

        [cmd("tool_bridge_project_opened")]
        private void ProjectOpened()
        {
            if (!McpServerBootstrap.Instance.ServerRunning && McpSettings.AutoRun)
                McpRun();
        }

        [cmd("tool_bridge_project_closed")]
        private void ProjectClosed()
        {
            ObjectStorage.Instance.Clear();
        }

        [cmd("tool_bridge_log")]
        private void EnableSystemLogging()
        {
            try
            {
                var expandedDirectoryPath = ProcessEnvironment.Current.ExpandEnvironmentVariables(SystemLogDirectoryPathTemplate);
                var directoryPath = Path.GetFullPath(expandedDirectoryPath);
                ToolBridgeLogger.Instance.EnableSystemLogging(directoryPath);
                ToolBridgeLogger.Instance.PublicInfo("System logging enabled. Log directory: " + directoryPath);
            }
            catch (Exception ex)
            {
                ToolBridgeLogger.Instance.PublicError("Failed to enable the Tool Bridge system log: " + ex.Message);
            }
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

        [cmd("mcp_control_panel")]
        private void McpControlPanel()
        {
            var runMcp = McpServerBootstrap.Instance.ServerRunning;
            var settings = new McpSettingsWrapper(runMcp);
            if (McpControlPanelDlg.Execute(settings, ref runMcp))
            {
                settings.SaveChanges();
                if (runMcp)
                {
                    if (!ToolBridgeBootstrap.Instance.ServerRunning)
                        ToolBridgeInit();
                    if (!McpServerBootstrap.Instance.ServerRunning)
                        McpServerRun();
                }
                else
                {
                    if (McpServerBootstrap.Instance.ServerRunning)
                        McpServerShutdown();
                    if (ToolBridgeBootstrap.Instance.ServerRunning)
                        ToolBridgeShutdown();
                }
            }
        }

        [cmd("mcp_tool_settings")]
        private void ToolSettings()
        {
            var toolSettings = new ToolSettingsWrapper();
            if (ToolSettingsDlg.Execute(toolSettings))
                toolSettings.SaveChanges();
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
