using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EloCalculator.UI
{
    /// <summary>A sidebar navigation entry with a glyph, a label and a selected state.</summary>
    internal sealed class NavButton : Control
    {
        private bool selected;
        private bool hovered;

        public NavButton(string glyph, string text)
        {
            Glyph = glyph;
            Text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Height = 42;
            Dock = DockStyle.Top;
            Cursor = Cursors.Hand;
            Font = Theme.Body;
            Margin = new Padding(0);
        }

        public string Glyph { get; }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Sidebar);

            var pill = new RectangleF(10, 3, Width - 20, Height - 6);
            if (selected || hovered)
            {
                using (var path = Theme.RoundedRect(pill, 8))
                using (var brush = new SolidBrush(selected ? Theme.Blend(Theme.Sidebar, Theme.Accent, 0.18f) : Theme.Surface))
                    g.FillPath(brush, path);
            }

            if (selected)
            {
                using (var bar = new SolidBrush(Theme.Accent))
                    g.FillRectangle(bar, 0, 10, 3, Height - 20);
            }

            var color = selected ? Theme.Text : Theme.TextMuted;
            TextRenderer.DrawText(g, Glyph, Theme.SectionHeading, new Rectangle(18, 0, 28, Height), selected ? Theme.Accent : Theme.TextFaint,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, Text, selected ? Theme.BodyBold : Theme.Body, new Rectangle(52, 0, Width - 60, Height), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }
}
