using System.IO;
using Topomatic.ApplicationEnvironment;
using Topomatic.Stg;

namespace Topomatic.ToolBridge.Settings.Implementation
{
    internal sealed class McpSettings : IMcpSettings
    {
        private const string SETTINGS_PATH = @"%UserAppDataPath%\Support\robur-mcp\settings\mcp_settings.rbobj";

        private const string DEFAULT_HOST = "127.0.0.1";
        private const string DEFAULT_PORT = "8000";

        private static readonly object m_SyncRoot = new object();

        public McpSettings()
        {
            Host = DEFAULT_HOST;
            Port = DEFAULT_PORT;
            AutoRun = false;
            Load();
        }

        public string Host { get; set; }
        public string Port { get; set; }
        public bool AutoRun { get; set; }

        public void Load()
        {
            lock (m_SyncRoot)
            {
                var settingsPath = ProcessEnvironment.Current.ExpandEnvironmentVariables(SETTINGS_PATH);
                if (File.Exists(settingsPath))
                {
                    var stgDocument = new StgDocument();
                    using (var stream = File.OpenRead(settingsPath))
                    {
                        stgDocument.LoadFromStreamAsBinary(stream);
                    }
                    var root = stgDocument.Body;
                    Host = root.Attribute.GetString("Host", DEFAULT_HOST);
                    Port = root.Attribute.GetString("Port", DEFAULT_PORT);
                    AutoRun = root.Attribute.GetBoolean("AutoRun", false);
                }
            }
        }

        public void Save()
        {
            lock (m_SyncRoot)
            {
                var settingsPath = ProcessEnvironment.Current.ExpandEnvironmentVariables(SETTINGS_PATH);
                var dirName = Path.GetDirectoryName(settingsPath);
                Directory.CreateDirectory(dirName);
                var stgDocument = new StgDocument();
                var root = stgDocument.Body;
                root.Attribute.AddString("Host", Host);
                root.Attribute.AddString("Port", Port);
                root.Attribute.AddBoolean("AutoRun", AutoRun);
                using (var stream = File.OpenWrite(settingsPath))
                {
                    stgDocument.SaveToStreamAsBinary(stream);
                }
            }
        }
    }
}
