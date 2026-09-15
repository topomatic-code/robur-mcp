namespace Topomatic.ToolBridge.Dialogs
{
    partial class ToolSettingsDlg
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
            this.toolsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.mainSplit = new System.Windows.Forms.SplitContainer();
            this.sideSplit = new System.Windows.Forms.SplitContainer();
            this.toolInspector = new Topomatic.Controls.ObjectInspection.PropertyInspector.PropertyInspector();
            this.schemaBox = new System.Windows.Forms.RichTextBox();
            this.lbInputSchema = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.mainSplit)).BeginInit();
            this.mainSplit.Panel1.SuspendLayout();
            this.mainSplit.Panel2.SuspendLayout();
            this.mainSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sideSplit)).BeginInit();
            this.sideSplit.Panel1.SuspendLayout();
            this.sideSplit.Panel2.SuspendLayout();
            this.sideSplit.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(700, 531);
            // 
            // btnOk
            // 
            this.btnOk.Location = new System.Drawing.Point(619, 531);
            // 
            // dividerLine
            // 
            this.dividerLine.Location = new System.Drawing.Point(0, 524);
            this.dividerLine.Size = new System.Drawing.Size(787, 2);
            // 
            // toolsPanel
            // 
            this.toolsPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.toolsPanel.AutoScroll = true;
            this.toolsPanel.BackColor = System.Drawing.SystemColors.Window;
            this.toolsPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.toolsPanel.Location = new System.Drawing.Point(0, 0);
            this.toolsPanel.Name = "toolsPanel";
            this.toolsPanel.Padding = new System.Windows.Forms.Padding(2);
            this.toolsPanel.Size = new System.Drawing.Size(358, 511);
            this.toolsPanel.TabIndex = 101;
            this.toolsPanel.WrapContents = false;
            // 
            // mainSplit
            // 
            this.mainSplit.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.mainSplit.Location = new System.Drawing.Point(7, 7);
            this.mainSplit.Name = "mainSplit";
            // 
            // mainSplit.Panel1
            // 
            this.mainSplit.Panel1.Controls.Add(this.toolsPanel);
            // 
            // mainSplit.Panel2
            // 
            this.mainSplit.Panel2.Controls.Add(this.sideSplit);
            this.mainSplit.Size = new System.Drawing.Size(775, 513);
            this.mainSplit.SplitterDistance = 360;
            this.mainSplit.TabIndex = 102;
            // 
            // sideSplit
            // 
            this.sideSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sideSplit.Location = new System.Drawing.Point(0, 0);
            this.sideSplit.Name = "sideSplit";
            this.sideSplit.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // sideSplit.Panel1
            // 
            this.sideSplit.Panel1.Controls.Add(this.toolInspector);
            // 
            // sideSplit.Panel2
            // 
            this.sideSplit.Panel2.Controls.Add(this.schemaBox);
            this.sideSplit.Panel2.Controls.Add(this.lbInputSchema);
            this.sideSplit.Size = new System.Drawing.Size(411, 513);
            this.sideSplit.SplitterDistance = 248;
            this.sideSplit.TabIndex = 0;
            // 
            // toolInspector
            // 
            this.toolInspector.Dock = System.Windows.Forms.DockStyle.Fill;
            this.toolInspector.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(238)))), ((int)(((byte)(237)))), ((int)(((byte)(232)))));
            this.toolInspector.DescriptionVisible = false;
            this.toolInspector.Location = new System.Drawing.Point(0, 0);
            this.toolInspector.Name = "toolInspector";
            this.toolInspector.SelectedIndex = -1;
            this.toolInspector.SelectMode = Topomatic.Controls.ObjectInspection.PropertyInspector.SelectMode.Parallel;
            this.toolInspector.Size = new System.Drawing.Size(411, 248);
            this.toolInspector.SpliterPosition = 206;
            this.toolInspector.TabIndex = 0;
            // 
            // schemaBox
            // 
            this.schemaBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.schemaBox.Location = new System.Drawing.Point(0, 17);
            this.schemaBox.Name = "schemaBox";
            this.schemaBox.ReadOnly = true;
            this.schemaBox.Size = new System.Drawing.Size(411, 244);
            this.schemaBox.TabIndex = 0;
            this.schemaBox.Text = "";
            // 
            // lbInputSchema
            // 
            this.lbInputSchema.Dock = System.Windows.Forms.DockStyle.Top;
            this.lbInputSchema.Location = new System.Drawing.Point(0, 0);
            this.lbInputSchema.Name = "lbInputSchema";
            this.lbInputSchema.Size = new System.Drawing.Size(411, 17);
            this.lbInputSchema.TabIndex = 1;
            this.lbInputSchema.Text = "Схема параметров";
            // 
            // ToolSettingsDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(787, 565);
            this.Controls.Add(this.mainSplit);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "ToolSettingsDlg";
            this.Text = "Настройки инструментов";
            this.Controls.SetChildIndex(this.btnCancel, 0);
            this.Controls.SetChildIndex(this.btnOk, 0);
            this.Controls.SetChildIndex(this.dividerLine, 0);
            this.Controls.SetChildIndex(this.mainSplit, 0);
            this.mainSplit.Panel1.ResumeLayout(false);
            this.mainSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.mainSplit)).EndInit();
            this.mainSplit.ResumeLayout(false);
            this.sideSplit.Panel1.ResumeLayout(false);
            this.sideSplit.Panel2.ResumeLayout(false);
            this.sideSplit.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sideSplit)).EndInit();
            this.sideSplit.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel toolsPanel;
        private System.Windows.Forms.SplitContainer mainSplit;
        private System.Windows.Forms.SplitContainer sideSplit;
        private Topomatic.Controls.ObjectInspection.PropertyInspector.PropertyInspector toolInspector;
        private System.Windows.Forms.RichTextBox schemaBox;
        private System.Windows.Forms.Label lbInputSchema;
    }
}
