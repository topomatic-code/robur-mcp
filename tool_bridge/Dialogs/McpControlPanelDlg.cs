using System.Windows.Forms;
using Topomatic.Controls.Dialogs;
using Topomatic.ToolBridge.Dialogs.Wrappers;

namespace Topomatic.ToolBridge.Dialogs
{
    internal partial class McpControlPanelDlg : SimpleDlg
    {
        public static bool Execute(McpSettingsWrapper settings, ref bool runMcp)
        {
            using (var dlg = new McpControlPanelDlg(settings, runMcp))
            {
                var result = dlg.ShowDialog() == DialogResult.OK;
                runMcp = dlg.tbRunMcp.Checked;
                return result;
            }
        }

        private readonly McpSettingsWrapper m_Settings;
        private readonly bool m_RunMcp;

        public McpControlPanelDlg(McpSettingsWrapper settings, bool runMcp)
        {
            m_Settings = settings;
            m_RunMcp = runMcp;
            InitializeComponent();
        }

        protected override void DoInit()
        {
            base.DoInit();
            tbRunMcp.Checked = m_RunMcp;
            piSettings.SelectedObjects = new object[] { m_Settings };
        }
    }
}
