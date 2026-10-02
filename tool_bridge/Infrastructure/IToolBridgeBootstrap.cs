namespace Topomatic.ToolBridge.Infrastructure
{
    internal interface IToolBridgeBootstrap
    {
        bool ServerRunning { get; }

        void Initialize();
        void Shutdown();
    }
}
