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
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = SystemColors.Window;
            Height = 44;
            Margin = new Padding(0, 0, 0, 1);
            MinimumSize = new Size(200, 44);
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

        private void SelectionControl_Click(object sender, EventArgs e) => SelectionRequested?.Invoke(this, EventArgs.Empty);
        private void ToggleButton_CheckedChanged(object sender, EventArgs e) => CheckedChanged?.Invoke(this, EventArgs.Empty);

        protected override void OnResize(EventArgs e)
        {
            const int horizontalPadding = 12;

            base.OnResize(e);

            if (m_Label == null || m_ToggleButton == null)
                return;
            m_ToggleButton.Location = new Point(
                ClientSize.Width - m_ToggleButton.Width - horizontalPadding,
                (ClientSize.Height - m_ToggleButton.Height) / 2
            );
            m_Label.SetBounds(
                horizontalPadding,
                0,
                Math.Max(0, m_ToggleButton.Left - horizontalPadding * 2),
                ClientSize.Height
            );
        }
    }
}
