using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Topomatic.ComponentModel;
using Topomatic.Controls.Dialogs;
using Topomatic.ToolBridge.Settings;

namespace Topomatic.ToolBridge.Dialogs.Wrappers
{
    internal sealed class McpSettingsWrapper
    {
        private const string CATEGORY_SERVER_STARTUP_PARAMETERS = "Параметры запуска сервера";
        private const string CATEGORY_SYSTEM_PARAMETERS = "Параметры системы";

        private static readonly Regex HostRegex = new Regex(
            @"^(?:(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)$",
            RegexOptions.CultureInvariant
        );

        private static readonly Regex PortRegex = new Regex(
            @"^(?:[1-9]\d{0,3}|[1-5]\d{4}|6[0-4]\d{3}|65[0-4]\d{2}|655[0-2]\d|6553[0-5])$",
            RegexOptions.CultureInvariant
        );

        private string m_Host;
        private string m_Port;

        public McpSettingsWrapper(bool mcpRunning)
        {
            McpRunning = mcpRunning;
            m_Host = McpSettings.Host;
            m_Port = McpSettings.Port;
            AutoRun = McpSettings.AutoRun;
        }

        [Obfuscation(Exclude = true, StripAfterObfuscation = true)]
        private bool McpRunning { get; set; }

        [Category(CATEGORY_SERVER_STARTUP_PARAMETERS)]
        [DisplayName("Хост")]
        [ConditionalReadOnly(nameof(McpRunning))]
        public string Host
        {
            get
            {
                return m_Host;
            }
            set
            {
                if (value == null || !HostRegex.IsMatch(value))
                {
                    MessageDlg.Show(
                        "Некорректное значение хоста. Укажите IPv4-адрес, например 127.0.0.1.",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }
                m_Host = value;
            }
        }

        [Category(CATEGORY_SERVER_STARTUP_PARAMETERS)]
        [DisplayName("Порт")]
        [ConditionalReadOnly(nameof(McpRunning))]
        public string Port
        {
            get
            {
                return m_Port;
            }
            set
            {
                if (value == null || !PortRegex.IsMatch(value))
                {
                    MessageDlg.Show(
                        "Некорректное значение порта. Укажите целое число от 1 до 65535.",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }
                m_Port = value;
            }
        }

        [Category(CATEGORY_SYSTEM_PARAMETERS)]
        [DisplayName("Автоматический запуск сервера")]
        public bool AutoRun { get; set; }

        public void SaveChanges()
        {
            McpSettings.Host = m_Host;
            McpSettings.Port = m_Port;
            McpSettings.AutoRun = AutoRun;
            McpSettings.Save();
        }
    }
}
