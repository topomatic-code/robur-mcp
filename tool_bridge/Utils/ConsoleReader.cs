using System;
using System.Text;
using Topomatic.Cad.Foundation;
using Topomatic.Cad.View;

namespace Topomatic.ToolBridge.Utils
{
    internal sealed class ConsoleReader : IConsole, IDisposable
    {
        private readonly StringBuilder m_StringBuilder;
        private readonly ConsoleListner m_Listner;

        public ConsoleReader()
        {
            m_StringBuilder = new StringBuilder();
            m_Listner = ConsoleListner.Current;
            m_Listner.Register(this);
        }

        public string RequestString { get; set; }
        public string ResponseString { get; set; }
        public string Content => m_StringBuilder.ToString();

        public void Commit() { }
        public void Write(string s) => m_StringBuilder.Append(s);
        public void WriteLine(string s) => m_StringBuilder.AppendLine(s);
        public void Dispose() => m_Listner.Unregister(this);
    }
}
