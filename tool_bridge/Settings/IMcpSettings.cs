namespace Topomatic.ToolBridge.Settings
{
    internal interface IMcpSettings
    {
        string Host { get; set; }
        string Port { get; set; }
        bool AutoRun { get; set; }

        void Save();
    }
}
