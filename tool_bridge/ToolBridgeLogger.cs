using System;
using System.Reflection;
using Topomatic.Cad.View;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public sealed class ToolBridgeLogger
    {
        private static ToolBridgeLogger m_Instance;

        public static ToolBridgeLogger Instance
        {
            get
            {
                if (m_Instance == null)
                    m_Instance = new ToolBridgeLogger();
                return m_Instance;
            }
        }

        private ToolBridgeLogger()
        {

        }

        public string CreateLogString(string message) => $"[{DateTime.Now:HH:mm:ss}] [Robur tool bridge]: {message}";
        public void Log(string message) => ConsoleListner.Current.WriteLine(CreateLogString(message));
    }
}
