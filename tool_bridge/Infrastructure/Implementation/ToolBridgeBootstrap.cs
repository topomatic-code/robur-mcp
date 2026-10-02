using Topomatic.ToolBridge.DependencyInjection;

namespace Topomatic.ToolBridge.Infrastructure.Implementation
{
    internal sealed class ToolBridgeBootstrap : IToolBridgeBootstrap
    {
        private readonly IContainer m_Container;
        private readonly IToolBridgeLogger m_Logger;

        private IToolBridgePipeServer m_Server;

        public ToolBridgeBootstrap([Singleton] IContainer container)
        {
            m_Container = container;
            m_Logger = container.GetSingleton<IToolBridgeLogger>();
        }

        public bool ServerRunning => m_Server != null;

        public void Initialize()
        {
            if (m_Server != null)
                return;

            var server = m_Container.CreateInstance<IToolBridgePipeServer>();
            try
            {
                server.Start();
                m_Server = server;
            }
            catch
            {
                server.Dispose();
                throw;
            }
            m_Logger.PublicInfo("Pipe server started.");
            m_Logger.SystemInfo("Pipe server started.");
        }

        public void Shutdown()
        {
            if (m_Server == null)
                return;

            m_Server.Dispose();
            m_Server = null;
            m_Logger.PublicInfo("Pipe server stopped.");
            m_Logger.SystemInfo("Pipe server stopped.");
        }
    }
}
