using System;
using Topomatic.Cad.View;
using Topomatic.ToolBridge.Services;

namespace Topomatic.ToolBridge
{
    internal sealed class ToolBridgeBootstrap
    {
        private static ToolBridgeBootstrap m_Instance;

        public static ToolBridgeBootstrap Instance
        {
            get
            {
                if (m_Instance == null)
                    m_Instance = new ToolBridgeBootstrap();
                return m_Instance;
            }
        }

        private readonly ToolBridgeLogger m_Logger;

        private ToolBridgePipeServer m_Server;

        private ToolBridgeBootstrap()
        {
            m_Logger = ToolBridgeLogger.Instance;
        }

        public bool ServerRunning => m_Server != null;

        public void Initialize(Func<CadView> cadViewProvider)
        {
            if (m_Server != null)
                return;
            var sessionStorage = new ObjectStorage();
            var toolManager = new ToolManager(sessionStorage, m_Logger, cadViewProvider);
            toolManager.Initialize();
            m_Server = new ToolBridgePipeServer("robur_tool_bridge", toolManager, m_Logger);
            m_Server.Start();
            m_Logger.Log("Pipe server started.");
        }

        public void Shutdown()
        {
            if (m_Server == null)
                return;
            m_Server.Dispose();
            m_Server = null;
            m_Logger.Log("Pipe server stopped.");
        }
    }
}
