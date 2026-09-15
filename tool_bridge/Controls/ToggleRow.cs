using System;
using System.Drawing;
using System.Windows.Forms;

namespace Topomatic.ToolBridge.Controls
{
    internal sealed class ToggleRow : UserControl
    {
        private readonly Label m_Label;
        private readonly ToggleButton m_ToggleButton;

        private bool m_Selected;

        public ToggleRow(string label, bool enabled)
        {
            SuspendLayout();
            // Use the same design-time font metrics as SimpleDlg and its derived dialogs.
            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Window;
            Height = 22;
            Margin = new Padding(0, 0, 0, 1);
            Padding = new Padding(6, 0, 6, 0);
            MinimumSize = new Size(100, 22);
            TabStop = false;
            m_Label = new Label
            {
                AutoEllipsis = true,
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft
            };
            m_ToggleButton = new ToggleButton
            {
                AccessibleName = $"Включить {label}",
                Checked = enabled,
                TabStop = true
            };
            Controls.Add(m_Label);
            Controls.Add(m_ToggleButton);
            Click += SelectionControl_Click;
            m_Label.Click += SelectionControl_Click;
            m_ToggleButton.Click += SelectionControl_Click;
            m_ToggleButton.Enter += SelectionControl_Click;
            m_ToggleButton.CheckedChanged += ToggleButton_CheckedChanged;
            ResumeLayout(true);
        }

        public event EventHandler SelectionRequested;
        public event EventHandler CheckedChanged;

        public bool Selected
        {
            get
            {
                return m_Selected;
            }
            set
            {
                if (m_Selected == value)
                    return;
                m_Selected = value;
                BackColor = value ? SystemColors.Highlight : SystemColors.Window;
                m_Label.ForeColor = value ? SystemColors.HighlightText : SystemColors.ControlText;
                Invalidate(true);
            }
        }

        public bool Checked => m_ToggleButton.Checked;

        public void FocusToggle() => m_ToggleButton.Focus();

        private void SelectionControl_Click(object sender, EventArgs e) => SelectionRequested?.Invoke(this, EventArgs.Empty);
        private void ToggleButton_CheckedChanged(object sender, EventArgs e) => CheckedChanged?.Invoke(this, EventArgs.Empty);

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);

            if (m_Label == null || m_ToggleButton == null)
                return;
            m_ToggleButton.Location = new Point(
                ClientSize.Width - m_ToggleButton.Width - Padding.Right,
                (ClientSize.Height - m_ToggleButton.Height) / 2
            );
            m_Label.SetBounds(
                Padding.Left,
                0,
                Math.Max(0, m_ToggleButton.Left - Padding.Left - Padding.Right),
                ClientSize.Height
            );
        }
    }
}
