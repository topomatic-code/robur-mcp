using System.IO;
using Topomatic.ApplicationEnvironment;
using Topomatic.Stg;

namespace Topomatic.ToolBridge.Settings
{
    internal static class McpSettings
    {
        private const string SETTINGS_PATH = @"%UserAppDataPath%\Support\robur-mcp\settings\mcp_settings.rbobj";

        private const string DEFAULT_HOST = "127.0.0.1";
        private const string DEFAULT_PORT = "8000";

        static McpSettings()
        {
            Host = DEFAULT_HOST;
            Port = DEFAULT_PORT;
            AutoRun = false;
            Load();
        }

        public static string Host { get; set; }
        public static string Port { get; set; }
        public static bool AutoRun { get; set; }

        public static void Load()
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

        public static void Save()
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
