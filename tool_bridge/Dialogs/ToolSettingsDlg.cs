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
                m_SelectedRow = null;
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

                var firstRow = toolsPanel.Controls.OfType<ToggleRow>().FirstOrDefault();
                if (firstRow != null)
                    SelectRow(firstRow);
            }
            finally
            {
                toolsPanel.ResumeLayout(true);
            }
        }

        private void ToolRow_SelectionRequested(object sender, EventArgs e)
        {
            var selectedRow = sender as ToggleRow;
            if (selectedRow == null)
                return;

            if (m_SelectedRow != selectedRow)
                SelectRow(selectedRow);
            selectedRow.FocusToggle();
        }

        private void SelectRow(ToggleRow selectedRow)
        {
            if (m_SelectedRow != null)
                m_SelectedRow.Selected = false;
            m_SelectedRow = selectedRow;
            m_SelectedRow.Selected = true;
            var toolConfig = (ToolConfigWrapper)m_SelectedRow.Tag;
            toolInspector.SelectedObjects = new object[] { toolConfig };
            schemaBox.Text = toolConfig.InputSchema;
            ApplyJsonStyle();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            const long previousKeyStateMask = 1L << 30;

            if (toolsPanel.ContainsFocus && (keyData == Keys.Up || keyData == Keys.Down))
            {
                var isAutoRepeat = (msg.LParam.ToInt64() & previousKeyStateMask) != 0;
                if (!isAutoRepeat)
                    MoveSelection(keyData == Keys.Up ? -1 : 1);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void MoveSelection(int offset)
        {
            var rows = toolsPanel.Controls.OfType<ToggleRow>().ToArray();
            if (rows.Length == 0)
                return;

            var currentIndex = Array.IndexOf(rows, m_SelectedRow);
            var nextIndex = currentIndex < 0
                ? (offset < 0 ? rows.Length - 1 : 0)
                : Math.Max(0, Math.Min(rows.Length - 1, currentIndex + offset));
            var nextRow = rows[nextIndex];

            if (nextRow == m_SelectedRow)
                return;

            SelectRow(nextRow);
            nextRow.FocusToggle();
            toolsPanel.ScrollControlIntoView(nextRow);
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
