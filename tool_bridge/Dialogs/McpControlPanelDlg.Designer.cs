namespace Topomatic.ToolBridge.Dialogs
{
    partial class McpControlPanelDlg
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tbRunMcp = new Topomatic.ToolBridge.Controls.ToggleButton();
            this.lblRunMcp = new System.Windows.Forms.Label();
            this.piSettings = new Topomatic.Controls.ObjectInspection.PropertyInspector.PropertyInspector();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(700, 562);
            // 
            // btnOk
            // 
            this.btnOk.Location = new System.Drawing.Point(538, 562);
            // 
            // dividerLine
            // 
            this.dividerLine.Location = new System.Drawing.Point(0, 547);
            this.dividerLine.Size = new System.Drawing.Size(874, 2);
            // 
            // tbRunMcp
            // 
            this.tbRunMcp.AccessibleName = "Запустить MCP-сервер";
            this.tbRunMcp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.tbRunMcp.BackColor = System.Drawing.Color.Transparent;
            this.tbRunMcp.Cursor = System.Windows.Forms.Cursors.Hand;
            this.tbRunMcp.Location = new System.Drawing.Point(794, 12);
            this.tbRunMcp.MinimumSize = new System.Drawing.Size(36, 20);
            this.tbRunMcp.Name = "tbRunMcp";
            this.tbRunMcp.Size = new System.Drawing.Size(56, 28);
            this.tbRunMcp.TabIndex = 101;
            this.tbRunMcp.UseVisualStyleBackColor = false;
            // 
            // lblRunMcp
            // 
            this.lblRunMcp.AutoSize = true;
            this.lblRunMcp.Location = new System.Drawing.Point(12, 15);
            this.lblRunMcp.Name = "lblRunMcp";
            this.lblRunMcp.Size = new System.Drawing.Size(244, 25);
            this.lblRunMcp.TabIndex = 102;
            this.lblRunMcp.Text = "Запустить MCP-сервер";
            // 
            // piSettings
            // 
            this.piSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.piSettings.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(238)))), ((int)(((byte)(237)))), ((int)(((byte)(232)))));
            this.piSettings.Location = new System.Drawing.Point(12, 52);
            this.piSettings.Name = "piSettings";
            this.piSettings.SelectedIndex = -1;
            this.piSettings.SelectMode = Topomatic.Controls.ObjectInspection.PropertyInspector.SelectMode.Parallel;
            this.piSettings.Size = new System.Drawing.Size(850, 486);
            this.piSettings.SpliterPosition = 425;
            this.piSettings.TabIndex = 103;
            // 
            // McpControlPanelDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(192F, 192F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(874, 629);
            this.Controls.Add(this.tbRunMcp);
            this.Controls.Add(this.lblRunMcp);
            this.Controls.Add(this.piSettings);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "McpControlPanelDlg";
            this.Text = "Панель управления Mcp-сервером";
            this.Controls.SetChildIndex(this.piSettings, 0);
            this.Controls.SetChildIndex(this.lblRunMcp, 0);
            this.Controls.SetChildIndex(this.tbRunMcp, 0);
            this.Controls.SetChildIndex(this.btnCancel, 0);
            this.Controls.SetChildIndex(this.btnOk, 0);
            this.Controls.SetChildIndex(this.dividerLine, 0);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Topomatic.ToolBridge.Controls.ToggleButton tbRunMcp;
        private System.Windows.Forms.Label lblRunMcp;
        private Topomatic.Controls.ObjectInspection.PropertyInspector.PropertyInspector piSettings;
    }
}
