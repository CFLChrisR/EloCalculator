using System.Drawing;
using System.Windows.Forms;

namespace EloCalculator.UI
{
    /// <summary>A dark, owner-drawn list box with roomier rows.</summary>
    internal sealed class ThemedListBox : ListBox
    {
        public ThemedListBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 28;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            BorderStyle = BorderStyle.None;
            Font = Theme.Body;
            IntegralHeight = false;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0)
                return;

            var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (var back = new SolidBrush(isSelected ? Theme.Blend(Theme.Surface, Theme.Accent, 0.3f) : Theme.Surface))
                e.Graphics.FillRectangle(back, e.Bounds);

            var bounds = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, bounds,
                isSelected ? Color.White : Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }
}
