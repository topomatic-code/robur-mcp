using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Topomatic.ToolBridge.Controls
{
    [DefaultEvent("CheckedChanged")]
    [DefaultProperty("Checked")]
    public sealed class ToggleButton : CheckBox
    {
        private static readonly Color CheckedColor = Color.FromArgb(76, 178, 160);
        private static readonly Color UncheckedColor = Color.FromArgb(145, 169, 205);
        private static readonly Color DisabledColor = Color.FromArgb(190, 190, 190);

        public ToggleButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.UserPaint,
                true
            );
            AutoSize = false;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            MinimumSize = new Size(36, 20);
            Size = new Size(56, 28);
            Text = string.Empty;
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override string Text
        {
            get
            {
                return base.Text;
            }
            set
            {
                base.Text = value;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaintBackground(e);

            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var outlineColor = Enabled
                ? (Checked ? CheckedColor : UncheckedColor)
                : DisabledColor;

            const float outlineWidth = 2.0f;
            var trackBounds = new RectangleF(
                outlineWidth / 2,
                outlineWidth / 2,
                Width - outlineWidth,
                Height - outlineWidth
            );

            using (var trackPath = CreateRoundedRectangle(trackBounds, trackBounds.Height / 2))
            using (var outlinePen = new Pen(outlineColor, outlineWidth))
            {
                graphics.DrawPath(outlinePen, trackPath);
            }

            var thumbPadding = 4.0f;
            var thumbSize = Height - 2 * thumbPadding;
            var thumbX = Checked
                ? Width - thumbPadding - thumbSize
                : thumbPadding;
            var thumbBounds = new RectangleF(thumbX, thumbPadding, thumbSize, thumbSize);

            using (var thumbBrush = new SolidBrush(outlineColor))
            {
                graphics.FillEllipse(thumbBrush, thumbBounds);
            }

            if (Focused && ShowFocusCues)
            {
                var focusBounds = Rectangle.Inflate(ClientRectangle, -1, -1);
                ControlPaint.DrawFocusRectangle(graphics, focusBounds);
            }
        }

        private static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
        {
            var diameter = radius * 2;
            var arcBounds = new RectangleF(bounds.Location, new SizeF(diameter, diameter));
            var path = new GraphicsPath();

            path.AddArc(arcBounds, 180, 90);
            arcBounds.X = bounds.Right - diameter;
            path.AddArc(arcBounds, 270, 90);
            arcBounds.Y = bounds.Bottom - diameter;
            path.AddArc(arcBounds, 0, 90);
            arcBounds.X = bounds.Left;
            path.AddArc(arcBounds, 90, 90);
            path.CloseFigure();

            return path;
        }
    }
}
