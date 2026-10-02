namespace Topomatic.ToolBridge.Infrastructure
{
    internal interface IMcpServerBootstrap
    {
        bool ServerRunning { get; }

        void Run();
        void Shutdown();
    }
}
