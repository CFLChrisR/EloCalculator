using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using EloCalculator.Core.Brackets;

namespace EloCalculator.UI.Bracket
{
    internal sealed class MatchClickedEventArgs : EventArgs
    {
        public MatchClickedEventArgs(BracketMatch match, Point location)
        {
            Match = match;
            Location = location;
        }

        public BracketMatch Match { get; }

        /// <summary>Where the click happened, in control coordinates.</summary>
        public Point Location { get; }
    }

    /// <summary>
    /// An interactive canvas for a double-elimination bracket. Drag to pan, scroll to move,
    /// Ctrl+scroll to zoom, and use the minimap in the corner to jump around. Hovering a
    /// player highlights their path through the bracket.
    /// </summary>
    internal sealed class BracketView : Control
    {
        private const float MinZoom = 0.2f;
        private const float MaxZoom = 2f;
        private const int MinimapMaxWidth = 220;
        private const int MinimapMaxHeight = 150;
        private const int MinimapMargin = 16;

        private readonly Timer animationTimer = new Timer { Interval = 15 };
        private DoubleEliminationBracket? bracket;
        private BracketLayout? layout;
        private float zoom = 1;
        private PointF offset;
        private RenderHighlight highlight;

        private Point pressPoint;
        private PointF pressOffset;
        private bool pressed;
        private bool dragging;
        private bool draggingMinimap;

        private DateTime animationStart;
        private float animationFromZoom;
        private float animationToZoom;
        private PointF animationFromOffset;
        private PointF animationToOffset;

        public BracketView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint | ControlStyles.Selectable, true);
            BackColor = Theme.Background;
            TabStop = true;
            animationTimer.Tick += (s, e) => StepAnimation();
        }

        public event EventHandler<MatchClickedEventArgs>? MatchClicked;

        /// <summary>Raised when the zoom level changes, for the toolbar's zoom readout.</summary>
        public event EventHandler? ZoomChanged;

        public float Zoom => zoom;

        public void SetBracket(DoubleEliminationBracket? value)
        {
            bracket = value;
            layout = value == null ? null : BracketLayout.Build(value);
            highlight = default;
            if (layout != null && IsHandleCreated)
                ShowOverview(animate: false);
            Invalidate();
        }

        /// <summary>Redraws after results change. The layout itself never moves.</summary>
        public void RefreshBracket() => Invalidate();

        public void ZoomBy(float factor) => ZoomAt(new PointF(Width / 2f, Height / 2f), zoom * factor, animate: true);

        public void ResetZoom() => ZoomAt(new PointF(Width / 2f, Height / 2f), 1f, animate: true);

        /// <summary>Fits the whole bracket on screen, but never zooms out so far that names are unreadable.</summary>
        public void FitToView()
        {
            if (layout == null)
                return;

            var fit = Math.Min(Width / layout.Size.Width, Height / layout.Size.Height);
            var target = Clamp(fit, MinZoom, 1f);
            AnimateTo(target, ClampOffset(CenteredOffset(new PointF(layout.Size.Width / 2, layout.Size.Height / 2), target), target));
        }

        public void ShowSection(BracketSide side)
        {
            var section = layout?.Sections.FirstOrDefault(s => s.Side == side);
            if (section != null)
                ShowArea(section.Bounds);
        }

        public void FocusMatch(BracketMatch match)
        {
            if (layout == null)
                return;

            var card = layout.GetCard(match.Id);
            var target = Math.Max(zoom, 0.85f);
            AnimateTo(target, ClampOffset(CenteredOffset(new PointF(card.X + card.Width / 2, card.Y + card.Height / 2), target), target));
            highlight.HoverMatchId = match.Id;
            Invalidate();
        }

        /// <summary>Renders the whole bracket to an image, independent of the current view.</summary>
        public Bitmap? RenderImage()
        {
            if (layout == null || bracket == null)
                return null;

            // Keep exports under ~8000px on the long side so they stay a sensible file size.
            var scale = Math.Min(2f, 8000f / Math.Max(layout.Size.Width, layout.Size.Height));
            var bitmap = new Bitmap((int)Math.Ceiling(layout.Size.Width * scale), (int)Math.Ceiling(layout.Size.Height * scale), PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Theme.Background);
                g.ScaleTransform(scale, scale);
                BracketRenderer.Draw(g, layout, bracket, default, new RectangleF(PointF.Empty, layout.Size));
            }

            return bitmap;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (layout != null)
                ShowOverview(animate: false);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            offset = ClampOffset(offset, zoom);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                animationTimer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Background);
            DrawDotGrid(g);

            if (layout == null || bracket == null)
            {
                DrawEmptyState(g);
                return;
            }

            var state = g.Save();
            g.TranslateTransform(offset.X, offset.Y);
            g.ScaleTransform(zoom, zoom);
            BracketRenderer.Draw(g, layout, bracket, highlight, VisibleWorld());
            g.Restore(state);

            if (ContentOverflows())
                DrawMinimap(g);
        }

        private void DrawDotGrid(Graphics g)
        {
            var spacing = 28 * zoom;
            if (spacing < 10)
                spacing *= 2;

            using (var brush = new SolidBrush(Theme.Blend(Theme.Background, Theme.Border, 0.45f)))
            {
                var startX = offset.X % spacing;
                var startY = offset.Y % spacing;
                for (var x = startX; x < Width; x += spacing)
                    for (var y = startY; y < Height; y += spacing)
                        g.FillRectangle(brush, x, y, 1.5f, 1.5f);
            }
        }

        private void DrawEmptyState(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var cx = Width / 2f;
            var cy = Height / 2f - 30;

            // A small bracket glyph.
            using (var pen = new Pen(Theme.TextFaint, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLines(pen, new[] { new PointF(cx - 60, cy - 30), new PointF(cx - 30, cy - 30), new PointF(cx - 30, cy + 30), new PointF(cx - 60, cy + 30) });
                g.DrawLine(pen, cx - 30, cy, cx, cy);
                g.DrawLines(pen, new[] { new PointF(cx + 60, cy - 30), new PointF(cx + 30, cy - 30), new PointF(cx + 30, cy + 30), new PointF(cx + 60, cy + 30) });
                g.DrawLine(pen, cx + 30, cy, cx, cy);
            }

            using (var trophy = new SolidBrush(Theme.Gold))
                g.FillEllipse(trophy, cx - 7, cy - 7, 14, 14);

            var titleRect = new Rectangle(0, (int)cy + 54, Width, 32);
            TextRenderer.DrawText(g, "No bracket yet", Theme.SectionHeading, titleRect, Theme.Text, TextFormatFlags.HorizontalCenter);
            var textRect = new Rectangle(0, (int)cy + 84, Width, 24);
            TextRenderer.DrawText(g, "Click New Bracket to seed a double-elimination tournament from your players.", Theme.Body, textRect, Theme.TextMuted, TextFormatFlags.HorizontalCenter);
        }

        private bool ContentOverflows() =>
            layout != null && (layout.Size.Width * zoom > Width + 1 || layout.Size.Height * zoom > Height + 1);

        private RectangleF MinimapBounds()
        {
            if (layout == null)
                return RectangleF.Empty;

            var scale = Math.Min(MinimapMaxWidth / layout.Size.Width, MinimapMaxHeight / layout.Size.Height);
            var w = layout.Size.Width * scale;
            var h = layout.Size.Height * scale;
            return new RectangleF(Width - w - MinimapMargin, Height - h - MinimapMargin, w, h);
        }

        private void DrawMinimap(Graphics g)
        {
            var bounds = MinimapBounds();
            var scale = bounds.Width / layout!.Size.Width;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.RoundedRect(RectangleF.Inflate(bounds, 6, 6), 10))
            {
                using (var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                    g.FillPath(shadow, path);
                using (var fill = new SolidBrush(Theme.WithAlpha(Theme.Sidebar, 235)))
                    g.FillPath(fill, path);
                using (var pen = new Pen(Theme.Border))
                    g.DrawPath(pen, path);
            }

            RectangleF ToMap(RectangleF r) => new RectangleF(bounds.X + r.X * scale, bounds.Y + r.Y * scale, Math.Max(1, r.Width * scale), Math.Max(1, r.Height * scale));

            foreach (var section in layout.Sections)
            {
                using (var brush = new SolidBrush(Theme.WithAlpha(BracketRenderer.SectionColor(section.Side), 30)))
                    g.FillRectangle(brush, ToMap(section.Bounds));
            }

            foreach (var match in bracket!.Matches)
            {
                Color color;
                switch (match.Status)
                {
                    case MatchStatus.Ready: color = Theme.Accent; break;
                    case MatchStatus.Completed: color = Theme.TextMuted; break;
                    case MatchStatus.Pending: color = Theme.Border; break;
                    default: color = Theme.WithAlpha(Theme.Border, 90); break;
                }

                using (var brush = new SolidBrush(color))
                    g.FillRectangle(brush, ToMap(layout.GetCard(match.Id)));
            }

            using (var brush = new SolidBrush(bracket.IsComplete ? Theme.Gold : Theme.Border))
                g.FillRectangle(brush, ToMap(layout.ChampionBounds));

            var view = ToMap(VisibleWorld());
            view.Intersect(bounds);
            using (var fill = new SolidBrush(Theme.WithAlpha(Theme.Accent, 30)))
                g.FillRectangle(fill, view);
            using (var pen = new Pen(Theme.Accent, 1.5f))
                g.DrawRectangle(pen, view.X, view.Y, view.Width, view.Height);
        }

        private RectangleF VisibleWorld() => new RectangleF(-offset.X / zoom, -offset.Y / zoom, Width / zoom, Height / zoom);

        private PointF ToWorld(Point p) => new PointF((p.X - offset.X) / zoom, (p.Y - offset.Y) / zoom);

        private PointF CenteredOffset(PointF worldCenter, float atZoom) =>
            new PointF(Width / 2f - worldCenter.X * atZoom, Height / 2f - worldCenter.Y * atZoom);

        /// <summary>Keeps the bracket on screen: centred when it fits, otherwise no further than its edges.</summary>
        private PointF ClampOffset(PointF value, float atZoom)
        {
            if (layout == null)
                return PointF.Empty;

            float ClampAxis(float v, float content, float viewport) =>
                content <= viewport ? (viewport - content) / 2 : Clamp(v, viewport - content, 0);

            return new PointF(
                ClampAxis(value.X, layout.Size.Width * atZoom, Width),
                ClampAxis(value.Y, layout.Size.Height * atZoom, Height));
        }

        private void ShowOverview(bool animate)
        {
            if (layout == null)
                return;

            // Start at a readable zoom on the top-left (first round) rather than a tiny overview.
            var fit = Math.Min(Width / layout.Size.Width, Height / layout.Size.Height);
            var target = Clamp(Math.Max(fit, 0.7f), MinZoom, 1f);
            var targetOffset = ClampOffset(PointF.Empty, target);
            if (animate)
            {
                AnimateTo(target, targetOffset);
            }
            else
            {
                zoom = target;
                offset = targetOffset;
                ZoomChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ShowArea(RectangleF area)
        {
            var padded = RectangleF.Inflate(area, 20, 20);
            var fit = Math.Min(Width / padded.Width, Height / padded.Height);
            var target = Clamp(Math.Max(fit, 0.6f), MinZoom, 1f);

            // Centre the area if it fits; otherwise show its top-left corner.
            var x = padded.Width * target <= Width ? padded.X + padded.Width / 2 : padded.X + Width / (2 * target);
            var y = padded.Height * target <= Height ? padded.Y + padded.Height / 2 : padded.Y + Height / (2 * target);
            AnimateTo(target, ClampOffset(CenteredOffset(new PointF(x, y), target), target));
        }

        private void ZoomAt(PointF anchor, float newZoom, bool animate)
        {
            newZoom = Clamp(newZoom, MinZoom, MaxZoom);
            var world = new PointF((anchor.X - offset.X) / zoom, (anchor.Y - offset.Y) / zoom);
            var newOffset = ClampOffset(new PointF(anchor.X - world.X * newZoom, anchor.Y - world.Y * newZoom), newZoom);
            if (animate)
            {
                AnimateTo(newZoom, newOffset);
            }
            else
            {
                animationTimer.Stop();
                zoom = newZoom;
                offset = newOffset;
                ZoomChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        private void AnimateTo(float targetZoom, PointF targetOffset)
        {
            animationFromZoom = zoom;
            animationToZoom = targetZoom;
            animationFromOffset = offset;
            animationToOffset = targetOffset;
            animationStart = DateTime.UtcNow;
            animationTimer.Start();
        }

        private void StepAnimation()
        {
            var t = (float)Math.Min(1, (DateTime.UtcNow - animationStart).TotalMilliseconds / 240);
            var eased = 1 - (float)Math.Pow(1 - t, 3);
            zoom = animationFromZoom + (animationToZoom - animationFromZoom) * eased;
            offset = new PointF(
                animationFromOffset.X + (animationToOffset.X - animationFromOffset.X) * eased,
                animationFromOffset.Y + (animationToOffset.Y - animationFromOffset.Y) * eased);
            if (t >= 1)
                animationTimer.Stop();
            ZoomChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        private void PanBy(float dx, float dy)
        {
            animationTimer.Stop();
            offset = ClampOffset(new PointF(offset.X + dx, offset.Y + dy), zoom);
            Invalidate();
        }

        private void JumpToMinimapPoint(Point p)
        {
            var bounds = MinimapBounds();
            var scale = bounds.Width / layout!.Size.Width;
            var world = new PointF((p.X - bounds.X) / scale, (p.Y - bounds.Y) / scale);
            animationTimer.Stop();
            offset = ClampOffset(CenteredOffset(world, zoom), zoom);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (layout == null)
                return;

            if (ContentOverflows() && RectangleF.Inflate(MinimapBounds(), 6, 6).Contains(e.Location))
            {
                draggingMinimap = true;
                JumpToMinimapPoint(e.Location);
                return;
            }

            pressed = true;
            dragging = false;
            pressPoint = e.Location;
            pressOffset = offset;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (layout == null || bracket == null)
                return;

            if (draggingMinimap)
            {
                JumpToMinimapPoint(e.Location);
                return;
            }

            if (pressed)
            {
                if (!dragging && (Math.Abs(e.X - pressPoint.X) > 4 || Math.Abs(e.Y - pressPoint.Y) > 4))
                    dragging = true;

                if (dragging)
                {
                    Cursor = Cursors.SizeAll;
                    animationTimer.Stop();
                    offset = ClampOffset(new PointF(pressOffset.X + e.X - pressPoint.X, pressOffset.Y + e.Y - pressPoint.Y), zoom);
                    Invalidate();
                    return;
                }
            }

            UpdateHover(e.Location);
        }

        private void UpdateHover(Point location)
        {
            var next = default(RenderHighlight);
            var overMinimap = ContentOverflows() && RectangleF.Inflate(MinimapBounds(), 6, 6).Contains(location);
            if (!overMinimap && layout!.HitTest(ToWorld(location), out var matchId, out var slot))
            {
                var match = bracket!.GetMatch(matchId);
                next.HoverMatchId = matchId;
                next.Entrant = match.GetEntrant(slot);
                Cursor = match.Status == MatchStatus.Ready || match.Status == MatchStatus.Completed ? Cursors.Hand : Cursors.Default;
            }
            else
            {
                Cursor = overMinimap ? Cursors.Hand : Cursors.Default;
            }

            if (next.HoverMatchId != highlight.HoverMatchId || !ReferenceEquals(next.Entrant, highlight.Entrant))
            {
                highlight = next;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            var wasClick = pressed && !dragging;
            pressed = false;
            dragging = false;
            draggingMinimap = false;
            Cursor = Cursors.Default;

            if (wasClick && layout != null && layout.HitTest(ToWorld(e.Location), out var matchId, out _))
                MatchClicked?.Invoke(this, new MatchClickedEventArgs(bracket!.GetMatch(matchId), e.Location));

            if (layout != null)
                UpdateHover(e.Location);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!pressed && (highlight.HoverMatchId != 0 || highlight.Entrant != null))
            {
                highlight = default;
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (layout == null)
                return;

            if ((ModifierKeys & Keys.Control) == Keys.Control)
                ZoomAt(e.Location, zoom * (float)Math.Pow(1.15, e.Delta / 120.0), animate: false);
            else if ((ModifierKeys & Keys.Shift) == Keys.Shift)
                PanBy(e.Delta, 0);
            else
                PanBy(0, e.Delta);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                    return true;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (layout == null)
                return;

            const float step = 80;
            switch (e.KeyCode)
            {
                case Keys.Left: PanBy(step, 0); break;
                case Keys.Right: PanBy(-step, 0); break;
                case Keys.Up: PanBy(0, step); break;
                case Keys.Down: PanBy(0, -step); break;
                case Keys.PageUp: PanBy(0, Height * 0.8f); break;
                case Keys.PageDown: PanBy(0, -Height * 0.8f); break;
                case Keys.Home: ShowOverview(animate: true); break;
                case Keys.Oemplus:
                case Keys.Add: ZoomBy(1.25f); break;
                case Keys.OemMinus:
                case Keys.Subtract: ZoomBy(0.8f); break;
                case Keys.D0:
                case Keys.NumPad0: ResetZoom(); break;
                case Keys.F: FitToView(); break;
                default: return;
            }

            e.Handled = true;
        }

        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}
