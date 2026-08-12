using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Topomatic.Controls.Dialogs;
using Topomatic.ToolBridge.Controls;
using Topomatic.ToolBridge.Dialogs.Wrappers;

namespace Topomatic.ToolBridge.Dialogs
{
    internal partial class ToolSettingsDlg : SimpleDlg
    {
        public static bool Execute(ToolSettingsWrapper toolSettings)
        {
            using (var dlg = new ToolSettingsDlg(toolSettings))
            {
                return dlg.ShowDialog() == DialogResult.OK;
            }
        }

        private readonly ToolSettingsWrapper m_ToolSettings;

        private ToggleRow m_SelectedRow;

        public ToolSettingsDlg(ToolSettingsWrapper toolSettings)
        {
            m_ToolSettings = toolSettings;
            InitializeComponent();
            toolsPanel.ClientSizeChanged += ToolsPanel_SizeChanged;
        }

        protected override void DoInit()
        {
            base.DoInit();
            toolsPanel.SuspendLayout();
            try
            {
                toolsPanel.Controls.Clear();
                foreach (var toolConfig in m_ToolSettings.ToolConfigs)
                {
                    var domain = string.IsNullOrWhiteSpace(toolConfig.Domain) ? "" : $"[{toolConfig.Domain}] ";
                    var row = new ToggleRow(domain + toolConfig.Name, toolConfig.Enabled);
                    row.Tag = toolConfig;
                    row.SelectionRequested += ToolRow_SelectionRequested;
                    row.CheckedChanged += ToolRow_CheckedChanged;
                    toolsPanel.Controls.Add(row);
                }
                UpdateRowWidths();
            }
            finally
            {
                toolsPanel.ResumeLayout(true);
            }
        }

        private void ToolRow_SelectionRequested(object sender, EventArgs e)
        {
            var selectedRow = sender as ToggleRow;
            if (selectedRow == null || m_SelectedRow == selectedRow)
                return;
            if (m_SelectedRow != null)
                m_SelectedRow.Selected = false;
            m_SelectedRow = selectedRow;
            m_SelectedRow.Selected = true;
            var toolConfig = (ToolConfigWrapper)m_SelectedRow.Tag;
            toolInspector.SelectedObjects = new object[] { toolConfig };
            schemaBox.Text = toolConfig.InputSchema;
            ApplyJsonStyle();
        }

        private void ApplyJsonStyle()
        {
            schemaBox.SelectAll();
            schemaBox.SelectionColor = schemaBox.ForeColor;
            var properties = Regex.Matches(schemaBox.Text, @"""(?:\\.|[^""\\])*""(?=\s*:)");
            foreach (Match property in properties)
            {
                schemaBox.Select(property.Index, property.Length);
                schemaBox.SelectionColor = Color.Blue;
            }
            schemaBox.Select(0, 0);
        }

        private void ToolRow_CheckedChanged(object sender, EventArgs e)
        {
            var selectedRow = sender as ToggleRow;
            if (selectedRow == null)
                return;
            var toolConfig = (ToolConfigWrapper)selectedRow.Tag;
            toolConfig.Enabled = selectedRow.Checked;
        }

        private void ToolsPanel_SizeChanged(object sender, EventArgs e) => UpdateRowWidths();

        private void UpdateRowWidths()
        {
            var width = Math.Max(100, toolsPanel.ClientSize.Width - toolsPanel.Padding.Horizontal);
            foreach (var row in toolsPanel.Controls.OfType<ToggleRow>())
            {
                row.Width = width;
            }
        }
    }
}
