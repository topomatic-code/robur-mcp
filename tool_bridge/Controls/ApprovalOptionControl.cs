using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Topomatic.ToolBridge.Controls
{
    internal sealed class ApprovalOptionControl : Control
    {
        private static readonly Color HoverColor = Color.FromArgb(0, 102, 204);

        private const int ArrowAreaWidth = 22;
        private const int HorizontalPadding = 4;

        private bool m_Hovered;

        public ApprovalOptionControl()
        {
            BackColor = SystemColors.Window;
            Height = 24;
            MinimumSize = new Size(360, 24);
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.UserPaint,
                true
            );
            TabStop = true;
        }

        public event EventHandler Selected;

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            m_Hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            m_Hovered = false;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left)
                OnSelected();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space)
                return;

            e.Handled = true;
            OnSelected();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaintBackground(e);

            var textColor = m_Hovered ? HoverColor : SystemColors.ControlText;
            if (m_Hovered)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var centerY = ClientSize.Height / 2;
                var arrow = new[]
                {
                    new Point(HorizontalPadding, centerY - 5),
                    new Point(HorizontalPadding, centerY + 5),
                    new Point(HorizontalPadding + 8, centerY)
                };
                using (var brush = new SolidBrush(HoverColor))
                {
                    e.Graphics.FillPolygon(brush, arrow);
                }
            }

            var textBounds = new Rectangle(
                ArrowAreaWidth,
                0,
                Math.Max(0, ClientSize.Width - ArrowAreaWidth),
                ClientSize.Height
            );
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textBounds,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
            );

            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle, textColor, BackColor);
        }

        private void OnSelected()
        {
            var handler = Selected;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
