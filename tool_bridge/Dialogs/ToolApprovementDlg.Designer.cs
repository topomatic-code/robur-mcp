namespace Topomatic.ToolBridge.Dialogs
{
    partial class ToolApprovementDlg
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.promptLabel = new System.Windows.Forms.Label();
            this.allowOnceOption = new Topomatic.ToolBridge.Controls.ApprovalOptionControl();
            this.denyOption = new Topomatic.ToolBridge.Controls.ApprovalOptionControl();
            this.allowForSessionOption = new Topomatic.ToolBridge.Controls.ApprovalOptionControl();
            this.allowPermanentlyOption = new Topomatic.ToolBridge.Controls.ApprovalOptionControl();
            this.SuspendLayout();
            // 
            // promptLabel
            // 
            this.promptLabel.AutoSize = true;
            this.promptLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.875F, System.Drawing.FontStyle.Bold);
            this.promptLabel.Location = new System.Drawing.Point(25, 25);
            this.promptLabel.Margin = new System.Windows.Forms.Padding(0, 0, 24, 0);
            this.promptLabel.MaximumSize = new System.Drawing.Size(1120, 0);
            this.promptLabel.Name = "promptLabel";
            this.promptLabel.Size = new System.Drawing.Size(718, 100);
            this.promptLabel.TabIndex = 0;
            this.promptLabel.Text = "Получен запрос на выполнение операции tool_name:\r\n\"Description\".\r\nДанная операция" +
    " может изменить состояние и данные проекта!\r\nПодтвердите выполнение операции:";
            this.promptLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.promptLabel.UseMnemonic = false;
            // 
            // allowOnceOption
            // 
            this.allowOnceOption.BackColor = System.Drawing.SystemColors.Window;
            this.allowOnceOption.Cursor = System.Windows.Forms.Cursors.Hand;
            this.allowOnceOption.Location = new System.Drawing.Point(5, 155);
            this.allowOnceOption.Margin = new System.Windows.Forms.Padding(0);
            this.allowOnceOption.MinimumSize = new System.Drawing.Size(360, 24);
            this.allowOnceOption.Name = "allowOnceOption";
            this.allowOnceOption.Size = new System.Drawing.Size(660, 24);
            this.allowOnceOption.TabIndex = 1;
            this.allowOnceOption.Text = "1. Разрешить один раз";
            this.allowOnceOption.Selected += new System.EventHandler(this.AllowOnceOption_Selected);
            // 
            // denyOption
            // 
            this.denyOption.BackColor = System.Drawing.SystemColors.Window;
            this.denyOption.Cursor = System.Windows.Forms.Cursors.Hand;
            this.denyOption.Location = new System.Drawing.Point(5, 275);
            this.denyOption.Margin = new System.Windows.Forms.Padding(0, 0, 0, 24);
            this.denyOption.MinimumSize = new System.Drawing.Size(360, 24);
            this.denyOption.Name = "denyOption";
            this.denyOption.Size = new System.Drawing.Size(660, 24);
            this.denyOption.TabIndex = 4;
            this.denyOption.Text = "4. Запретить";
            this.denyOption.Selected += new System.EventHandler(this.DenyOption_Selected);
            // 
            // allowForSessionOption
            // 
            this.allowForSessionOption.BackColor = System.Drawing.SystemColors.Window;
            this.allowForSessionOption.Cursor = System.Windows.Forms.Cursors.Hand;
            this.allowForSessionOption.Location = new System.Drawing.Point(5, 195);
            this.allowForSessionOption.Margin = new System.Windows.Forms.Padding(0);
            this.allowForSessionOption.MinimumSize = new System.Drawing.Size(360, 24);
            this.allowForSessionOption.Name = "allowForSessionOption";
            this.allowForSessionOption.Size = new System.Drawing.Size(660, 24);
            this.allowForSessionOption.TabIndex = 2;
            this.allowForSessionOption.Text = "2. Разрешить в текущей сессии";
            this.allowForSessionOption.Selected += new System.EventHandler(this.AllowForSessionOption_Selected);
            // 
            // allowPermanentlyOption
            // 
            this.allowPermanentlyOption.BackColor = System.Drawing.SystemColors.Window;
            this.allowPermanentlyOption.Cursor = System.Windows.Forms.Cursors.Hand;
            this.allowPermanentlyOption.Location = new System.Drawing.Point(5, 235);
            this.allowPermanentlyOption.Margin = new System.Windows.Forms.Padding(0);
            this.allowPermanentlyOption.MinimumSize = new System.Drawing.Size(360, 24);
            this.allowPermanentlyOption.Name = "allowPermanentlyOption";
            this.allowPermanentlyOption.Size = new System.Drawing.Size(660, 24);
            this.allowPermanentlyOption.TabIndex = 3;
            this.allowPermanentlyOption.Text = "3. Разрешить навсегда";
            this.allowPermanentlyOption.Selected += new System.EventHandler(this.AllowPermanentlyOption_Selected);
            // 
            // ToolApprovementDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(192F, 192F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(771, 325);
            this.Controls.Add(this.promptLabel);
            this.Controls.Add(this.allowOnceOption);
            this.Controls.Add(this.denyOption);
            this.Controls.Add(this.allowForSessionOption);
            this.Controls.Add(this.allowPermanentlyOption);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Margin = new System.Windows.Forms.Padding(6);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ToolApprovementDlg";
            this.Padding = new System.Windows.Forms.Padding(2);
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.ToolApprovementDlg_Paint);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label promptLabel;
        private Topomatic.ToolBridge.Controls.ApprovalOptionControl allowOnceOption;
        private Topomatic.ToolBridge.Controls.ApprovalOptionControl allowForSessionOption;
        private Topomatic.ToolBridge.Controls.ApprovalOptionControl allowPermanentlyOption;
        private Topomatic.ToolBridge.Controls.ApprovalOptionControl denyOption;
    }
}
