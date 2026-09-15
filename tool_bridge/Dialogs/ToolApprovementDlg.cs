using System;
using System.Drawing;
using System.Windows.Forms;
using Topomatic.ToolBridge.Dialogs.Results;

namespace Topomatic.ToolBridge.Dialogs
{
    public sealed partial class ToolApprovementDlg : Form
    {
        public static ToolApprovementResult Execute(string toolName, string toolDescription)
        {
            using (var dlg = new ToolApprovementDlg(toolName, toolDescription))
            {
                dlg.ShowDialog();
                return dlg.m_Result;
            }
        }

        private ToolApprovementResult m_Result = ToolApprovementResult.Deny;

        private ToolApprovementDlg(string toolName, string toolDescription)
        {
            const int DESCRIPTION_LENGTH = 85;

            InitializeComponent();

            string description;
            if (toolDescription.Length < DESCRIPTION_LENGTH)
                description = toolDescription;
            else
                description = toolDescription.Substring(0, DESCRIPTION_LENGTH - 3) + "...";

            promptLabel.Text = $"Получен запрос на выполнение операции {toolName}:\n" +
                $"\"{description}\".\n" +
                $"Данная операция может изменить состояние и данные проекта!\n" +
                $"Подтвердите выполнение операции:";
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            ToolApprovementResult result;
            switch (keyData)
            {
                case Keys.D1:
                case Keys.NumPad1:
                    result = ToolApprovementResult.AllowOnce;
                    break;
                case Keys.D2:
                case Keys.NumPad2:
                    result = ToolApprovementResult.AllowForSession;
                    break;
                case Keys.D3:
                case Keys.NumPad3:
                    result = ToolApprovementResult.AllowPermanently;
                    break;
                case Keys.D4:
                case Keys.NumPad4:
                    result = ToolApprovementResult.Deny;
                    break;
                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }

            SelectResult(result);
            return true;
        }

        private void AllowOnceOption_Selected(object sender, EventArgs e)
        {
            SelectResult(ToolApprovementResult.AllowOnce);
        }

        private void AllowForSessionOption_Selected(object sender, EventArgs e)
        {
            SelectResult(ToolApprovementResult.AllowForSession);
        }

        private void AllowPermanentlyOption_Selected(object sender, EventArgs e)
        {
            SelectResult(ToolApprovementResult.AllowPermanently);
        }

        private void DenyOption_Selected(object sender, EventArgs e)
        {
            SelectResult(ToolApprovementResult.Deny);
        }

        private void SelectResult(ToolApprovementResult result)
        {
            m_Result = result;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ToolApprovementDlg_Paint(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(SystemColors.ActiveBorder))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            }
        }
    }
}
