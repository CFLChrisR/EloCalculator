using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EloCalculator.UI
{
    internal enum ButtonKind
    {
        Primary,
        Secondary,
        Danger,
    }

    /// <summary>Colours, fonts and control styling shared by the whole app.</summary>
    internal static class Theme
    {
        public static readonly Color Background = Hex(0x111318);
        public static readonly Color Sidebar = Hex(0x171A21);
        public static readonly Color Surface = Hex(0x1D2129);
        public static readonly Color SurfaceRaised = Hex(0x262B35);
        public static readonly Color SurfaceHover = Hex(0x2D3341);
        public static readonly Color Border = Hex(0x323846);
        public static readonly Color Text = Hex(0xE7E9EE);
        public static readonly Color TextMuted = Hex(0x9097A8);
        public static readonly Color TextFaint = Hex(0x5F6678);
        public static readonly Color Accent = Hex(0x5B8CFF);
        public static readonly Color AccentHover = Hex(0x7AA2FF);
        public static readonly Color Success = Hex(0x3DD68C);
        public static readonly Color Danger = Hex(0xFF6B6B);
        public static readonly Color Gold = Hex(0xF5B841);
        public static readonly Color Silver = Hex(0xC4CAD6);
        public static readonly Color Bronze = Hex(0xD48A5A);
        public static readonly Color Losers = Hex(0xB07CFF);

        public static readonly Font Body = new Font("Segoe UI", 9.75f);
        public static readonly Font BodyBold = new Font("Segoe UI Semibold", 9.75f);
        public static readonly Font Small = new Font("Segoe UI", 8.25f);
        public static readonly Font Heading = new Font("Segoe UI Semibold", 18f);
        public static readonly Font SectionHeading = new Font("Segoe UI Semibold", 11f);

        public static Color Hex(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

        public static Color WithAlpha(Color color, int alpha) => Color.FromArgb(alpha, color);

        /// <summary>Mixes <paramref name="color"/> towards <paramref name="target"/> by <paramref name="amount"/> (0 to 1).</summary>
        public static Color Blend(Color color, Color target, float amount) => Color.FromArgb(
            (int)(color.A + (target.A - color.A) * amount),
            (int)(color.R + (target.R - color.R) * amount),
            (int)(color.G + (target.G - color.G) * amount),
            (int)(color.B + (target.B - color.B) * amount));

        public static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            var d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            if (d <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Button CreateButton(string text, ButtonKind kind = ButtonKind.Secondary)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 34),
                Padding = new Padding(12, 2, 12, 2),
                Margin = new Padding(0, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                Font = kind == ButtonKind.Primary ? BodyBold : Body,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };

            Color back, hover, fore, border;
            switch (kind)
            {
                case ButtonKind.Primary:
                    back = Accent; hover = AccentHover; fore = Color.White; border = Accent;
                    break;
                case ButtonKind.Danger:
                    back = SurfaceRaised; hover = Blend(SurfaceRaised, Danger, 0.25f); fore = Danger; border = Blend(Border, Danger, 0.4f);
                    break;
                default:
                    back = SurfaceRaised; hover = SurfaceHover; fore = Text; border = Border;
                    break;
            }

            button.BackColor = back;
            button.ForeColor = fore;
            button.FlatAppearance.BorderColor = border;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = hover;
            button.FlatAppearance.MouseDownBackColor = Blend(hover, Color.Black, 0.15f);
            return button;
        }

        public static TextBox CreateTextBox(string placeholder = "")
        {
            var box = new TextBox
            {
                BackColor = SurfaceRaised,
                ForeColor = Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11f),
                Margin = new Padding(0, 0, 8, 0),
            };
#if NET
            box.PlaceholderText = placeholder;
#endif
            return box;
        }

        public static ComboBox CreateComboBox()
        {
            return new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = SurfaceRaised,
                ForeColor = Text,
                Font = new Font("Segoe UI", 11f),
                Margin = new Padding(0, 4, 0, 0),
            };
        }

        public static Label CreateLabel(string text, Font? font = null, Color? color = null)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = font ?? Body,
                ForeColor = color ?? Text,
                BackColor = Color.Transparent,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 0, 4),
            };
        }

        public static FlowLayoutPanel CreateButtonRow()
        {
            return new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 8, 0, 0),
                BackColor = Color.Transparent,
            };
        }

        public static DataGridView CreateGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Surface,
                GridColor = Border,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 38,
                Font = Body,
                Margin = new Padding(0),
            };

            grid.RowTemplate.Height = 34;
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = SurfaceRaised,
                ForeColor = TextMuted,
                SelectionBackColor = SurfaceRaised,
                SelectionForeColor = TextMuted,
                Font = BodyBold,
                Padding = new Padding(8, 0, 8, 0),
            };
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Surface,
                ForeColor = Text,
                SelectionBackColor = Blend(Surface, Accent, 0.3f),
                SelectionForeColor = Color.White,
                Font = Body,
                Padding = new Padding(8, 0, 8, 0),
            };
            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Blend(Surface, SurfaceRaised, 0.4f), Font = Body };
            return grid;
        }

        public static DataGridViewTextBoxColumn AddColumn(DataGridView grid, string header, float weight, DataGridViewContentAlignment alignment = DataGridViewContentAlignment.MiddleLeft)
        {
            var column = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                FillWeight = weight,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            };
            column.DefaultCellStyle.Alignment = alignment;
            column.HeaderCell.Style.Alignment = alignment;
            grid.Columns.Add(column);
            return column;
        }

        public static ContextMenuStrip CreateContextMenu()
        {
            return new ContextMenuStrip
            {
                Renderer = new DarkMenuRenderer(),
                BackColor = SurfaceRaised,
                ForeColor = Text,
                Font = Body,
                ShowImageMargin = false,
                Padding = new Padding(2, 4, 2, 4),
            };
        }

        private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
        {
            public DarkMenuRenderer() : base(new DarkColors())
            {
                RoundedEdges = false;
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? (e.Item.Tag as Color? ?? Text) : TextFaint;
                base.OnRenderItemText(e);
            }
        }

        private sealed class DarkColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => SurfaceRaised;
            public override Color MenuBorder => Border;
            public override Color MenuItemBorder => Blend(SurfaceHover, Accent, 0.3f);
            public override Color MenuItemSelected => SurfaceHover;
            public override Color MenuItemSelectedGradientBegin => SurfaceHover;
            public override Color MenuItemSelectedGradientEnd => SurfaceHover;
            public override Color SeparatorDark => Border;
            public override Color SeparatorLight => Border;
            public override Color ImageMarginGradientBegin => SurfaceRaised;
            public override Color ImageMarginGradientMiddle => SurfaceRaised;
            public override Color ImageMarginGradientEnd => SurfaceRaised;
        }
    }
}
