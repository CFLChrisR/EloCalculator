using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using EloCalculator.Core.Brackets;

namespace EloCalculator.UI.Bracket
{
    /// <summary>What the pointer is over, so the renderer can highlight it.</summary>
    internal struct RenderHighlight
    {
        public Entrant? Entrant;
        public int HoverMatchId;
    }

    /// <summary>Draws a bracket in world coordinates. Used for the screen and for image export.</summary>
    internal static class BracketRenderer
    {
        // Fonts are in pixels so they scale with the world transform rather than the screen DPI.
        private static readonly Font NameFont = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Pixel);
        private static readonly Font NameBoldFont = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel);
        private static readonly Font PendingFont = new Font("Segoe UI", 12f, FontStyle.Italic, GraphicsUnit.Pixel);
        private static readonly Font SeedFont = new Font("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        private static readonly Font LabelFont = new Font("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        private static readonly Font HeaderFont = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel);
        private static readonly Font SectionFont = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel);
        private static readonly Font ChampionCaptionFont = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel);
        private static readonly Font ChampionNameFont = new Font("Segoe UI", 22f, FontStyle.Bold, GraphicsUnit.Pixel);

        private static readonly StringFormat LeftMiddle = new StringFormat(StringFormatFlags.NoWrap)
        {
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
        };

        private static readonly StringFormat CenterMiddle = new StringFormat(StringFormatFlags.NoWrap)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
        };

        private static readonly StringFormat RightMiddle = new StringFormat(StringFormatFlags.NoWrap)
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center,
        };

        public static Color SectionColor(BracketSide side) =>
            side == BracketSide.Winners ? Theme.Accent : side == BracketSide.Losers ? Theme.Losers : Theme.Gold;

        public static void Draw(Graphics g, BracketLayout layout, DoubleEliminationBracket bracket, RenderHighlight highlight, RectangleF visible)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            foreach (var section in layout.Sections)
                if (section.Bounds.IntersectsWith(visible))
                    DrawSection(g, section);

            foreach (var header in layout.Headers)
                if (header.Bounds.IntersectsWith(visible))
                    DrawRoundHeader(g, header);

            foreach (var match in bracket.Matches)
                DrawConnectors(g, layout, bracket, match, highlight);

            foreach (var match in bracket.Matches)
            {
                var rect = layout.GetCard(match.Id);
                var withLabel = RectangleF.FromLTRB(rect.Left, rect.Top - BracketLayout.LabelHeight, rect.Right, rect.Bottom + 4);
                if (withLabel.IntersectsWith(visible))
                    DrawCard(g, bracket, match, rect, highlight);
            }

            if (layout.ChampionBounds.IntersectsWith(visible))
                DrawChampion(g, layout.ChampionBounds, bracket);
        }

        private static void DrawSection(Graphics g, BracketSection section)
        {
            var color = SectionColor(section.Side);
            using (var path = Theme.RoundedRect(section.Bounds, 14))
            {
                using (var fill = new SolidBrush(Theme.WithAlpha(color, 10)))
                    g.FillPath(fill, path);
                using (var border = new Pen(Theme.WithAlpha(color, 40)))
                    g.DrawPath(border, path);
            }

            var titleRect = new RectangleF(section.Bounds.X + BracketLayout.SectionPadding, section.Bounds.Y + 6, 300, BracketLayout.SectionTitleHeight - 8);
            using (var dot = new SolidBrush(color))
                g.FillEllipse(dot, titleRect.X, titleRect.Y + titleRect.Height / 2 - 4, 8, 8);
            using (var brush = new SolidBrush(color))
                g.DrawString(section.Title, SectionFont, brush, new RectangleF(titleRect.X + 16, titleRect.Y, titleRect.Width, titleRect.Height), LeftMiddle);
        }

        private static void DrawRoundHeader(Graphics g, RoundHeader header)
        {
            using (var path = Theme.RoundedRect(header.Bounds, header.Bounds.Height / 2))
            using (var fill = new SolidBrush(Theme.WithAlpha(Theme.SurfaceRaised, 200)))
                g.FillPath(fill, path);
            using (var brush = new SolidBrush(Theme.TextMuted))
                g.DrawString(header.Text.ToUpperInvariant(), HeaderFont, brush, header.Bounds, CenterMiddle);
        }

        private static void DrawConnectors(Graphics g, BracketLayout layout, DoubleEliminationBracket bracket, BracketMatch match, RenderHighlight highlight)
        {
            var target = layout.GetCard(match.Id);
            for (var slot = 0; slot < 2; slot++)
            {
                var source = match.GetSource(slot);
                if (source.Kind != SlotSourceKind.WinnerOf)
                    continue;
                if (ReferenceEquals(match, bracket.GrandFinalReset) && match.Status == MatchStatus.NotNeeded)
                    continue;

                var feeder = bracket.GetMatch(source.Value);
                var from = layout.GetCard(feeder.Id);
                var start = new PointF(from.Right, from.Top + BracketLayout.CardHeight / 2);
                var end = new PointF(target.Left, target.Top + BracketLayout.RowHeight * (slot + 0.5f));
                var midX = target.Left - BracketLayout.ColumnGap / 2;

                var advanced = feeder.IsDecided && feeder.Winner != null;
                var onPath = advanced && highlight.Entrant != null && ReferenceEquals(feeder.Winner, highlight.Entrant);
                var color = onPath ? Theme.Accent : advanced ? Theme.Blend(Theme.Border, Theme.TextFaint, 0.6f) : Theme.Border;
                if (feeder.Status == MatchStatus.Bye && feeder.Winner == null)
                    color = Theme.WithAlpha(Theme.Border, 90);

                using (var pen = new Pen(color, onPath ? 2.5f : 1.5f))
                using (var path = new GraphicsPath())
                {
                    pen.LineJoin = LineJoin.Round;
                    var r = Math.Min(8f, Math.Abs(end.Y - start.Y) / 2);
                    var down = end.Y >= start.Y ? 1 : -1;
                    path.AddLine(start.X, start.Y, midX - r, start.Y);
                    if (r > 0.5f)
                    {
                        path.AddBezier(midX - r, start.Y, midX, start.Y, midX, start.Y, midX, start.Y + r * down);
                        path.AddLine(midX, start.Y + r * down, midX, end.Y - r * down);
                        path.AddBezier(midX, end.Y - r * down, midX, end.Y, midX, end.Y, midX + r, end.Y);
                    }

                    path.AddLine(midX + r, end.Y, end.X, end.Y);
                    g.DrawPath(pen, path);
                }
            }
        }

        private static void DrawCard(Graphics g, DoubleEliminationBracket bracket, BracketMatch match, RectangleF rect, RenderHighlight highlight)
        {
            var faded = match.Status == MatchStatus.Bye || match.Status == MatchStatus.NotNeeded;
            var hovered = highlight.HoverMatchId == match.Id;
            var involved = match.Involves(highlight.Entrant);

            DrawCardLabel(g, match, rect, faded);

            using (var path = Theme.RoundedRect(rect, 8))
            {
                if (!faded)
                {
                    using (var shadowPath = Theme.RoundedRect(new RectangleF(rect.X, rect.Y + 3, rect.Width, rect.Height), 8))
                    using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                        g.FillPath(shadow, shadowPath);
                }

                var fill = faded ? Theme.Blend(Theme.Surface, Theme.Background, 0.55f)
                    : hovered && (match.Status == MatchStatus.Ready || match.Status == MatchStatus.Completed) ? Theme.SurfaceHover
                    : Theme.Surface;
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, path);

                var state = g.Save();
                g.SetClip(path);
                for (var slot = 0; slot < 2; slot++)
                    DrawSlot(g, bracket, match, slot, new RectangleF(rect.X, rect.Y + slot * BracketLayout.RowHeight, rect.Width, BracketLayout.RowHeight), highlight, faded);
                g.Restore(state);

                using (var divider = new Pen(faded ? Theme.WithAlpha(Theme.Border, 120) : Theme.Border))
                    g.DrawLine(divider, rect.X + 1, rect.Y + BracketLayout.RowHeight, rect.Right - 1, rect.Y + BracketLayout.RowHeight);

                Color borderColor;
                float borderWidth = 1;
                if (match.Status == MatchStatus.Ready)
                {
                    borderColor = Theme.Accent;
                    borderWidth = hovered ? 2.5f : 1.75f;
                }
                else if (involved)
                {
                    borderColor = Theme.Blend(Theme.Border, Theme.Accent, 0.6f);
                    borderWidth = 1.5f;
                }
                else
                {
                    borderColor = faded ? Theme.WithAlpha(Theme.Border, 120) : Theme.Border;
                }

                using (var pen = new Pen(borderColor, borderWidth))
                    g.DrawPath(pen, path);
            }
        }

        private static void DrawCardLabel(Graphics g, BracketMatch match, RectangleF rect, bool faded)
        {
            var labelRect = new RectangleF(rect.X + 2, rect.Y - BracketLayout.LabelHeight, rect.Width - 4, BracketLayout.LabelHeight - 2);
            using (var brush = new SolidBrush(faded ? Theme.WithAlpha(Theme.TextFaint, 140) : Theme.TextFaint))
                g.DrawString(match.Label, LabelFont, brush, labelRect, LeftMiddle);

            string? status = null;
            var statusColor = Theme.TextFaint;
            switch (match.Status)
            {
                case MatchStatus.Ready:
                    status = "● READY";
                    statusColor = Theme.Accent;
                    break;
                case MatchStatus.Completed:
                    status = "FINAL";
                    break;
                case MatchStatus.Bye:
                    status = "BYE";
                    break;
                case MatchStatus.NotNeeded:
                    status = "NOT NEEDED";
                    break;
            }

            if (status != null)
            {
                using (var brush = new SolidBrush(faded ? Theme.WithAlpha(statusColor, 140) : statusColor))
                    g.DrawString(status, LabelFont, brush, labelRect, RightMiddle);
            }
        }

        private static void DrawSlot(Graphics g, DoubleEliminationBracket bracket, BracketMatch match, int slot, RectangleF row, RenderHighlight highlight, bool faded)
        {
            var entrant = match.GetEntrant(slot);
            var state = match.GetState(slot);
            var isWinner = match.Status == MatchStatus.Completed && match.WinnerSlot == slot;
            var isLoser = match.Status == MatchStatus.Completed && match.WinnerSlot != slot;
            var highlighted = entrant != null && ReferenceEquals(entrant, highlight.Entrant);

            if (highlighted)
            {
                using (var brush = new SolidBrush(Theme.WithAlpha(Theme.Accent, 45)))
                    g.FillRectangle(brush, row);
            }

            if (isWinner)
            {
                using (var bar = new SolidBrush(Theme.Success))
                    g.FillRectangle(bar, row.X, row.Y, 4, row.Height);
            }

            // Seed badge.
            var badge = new RectangleF(row.X + 10, row.Y + (row.Height - 18) / 2, 24, 18);
            if (entrant != null)
            {
                using (var path = Theme.RoundedRect(badge, 4))
                using (var brush = new SolidBrush(isWinner ? Theme.Blend(Theme.SurfaceRaised, Theme.Success, 0.25f) : Theme.SurfaceRaised))
                    g.FillPath(brush, path);
                using (var brush = new SolidBrush(faded ? Theme.TextFaint : Theme.TextMuted))
                    g.DrawString(entrant.Seed.ToString(), SeedFont, brush, badge, CenterMiddle);
            }

            var textRect = new RectangleF(badge.Right + 8, row.Y, row.Right - badge.Right - 40, row.Height);
            string text;
            Font font;
            Color color;
            if (state == SlotState.Filled && entrant != null)
            {
                text = entrant.Name;
                font = isWinner ? NameBoldFont : NameFont;
                color = faded ? Theme.TextMuted : isLoser ? Theme.TextFaint : Theme.Text;
            }
            else if (state == SlotState.Bye)
            {
                text = "Bye";
                font = PendingFont;
                color = Theme.TextFaint;
            }
            else
            {
                text = bracket.DescribeSource(match.GetSource(slot));
                font = PendingFont;
                color = Theme.TextFaint;
                textRect.X = row.X + 12;
                textRect.Width = row.Width - 24;
            }

            using (var brush = new SolidBrush(color))
                g.DrawString(text, font, brush, textRect, LeftMiddle);

            if (match.Status == MatchStatus.Completed)
            {
                var markRect = new RectangleF(row.Right - 34, row.Y, 24, row.Height);
                using (var brush = new SolidBrush(isWinner ? Theme.Success : Theme.WithAlpha(Theme.Danger, 150)))
                    g.DrawString(isWinner ? "W" : "L", NameBoldFont, brush, markRect, CenterMiddle);
            }
        }

        private static void DrawChampion(Graphics g, RectangleF rect, DoubleEliminationBracket bracket)
        {
            var champion = bracket.Champion;
            var accent = champion != null ? Theme.Gold : Theme.Border;

            using (var path = Theme.RoundedRect(rect, 14))
            {
                if (champion != null)
                {
                    using (var glowPath = Theme.RoundedRect(RectangleF.Inflate(rect, 6, 6), 18))
                    using (var glow = new SolidBrush(Theme.WithAlpha(Theme.Gold, 28)))
                        g.FillPath(glow, glowPath);
                }

                using (var fill = new LinearGradientBrush(rect, Theme.Blend(Theme.Surface, accent, 0.12f), Theme.Surface, LinearGradientMode.Vertical))
                    g.FillPath(fill, path);
                using (var pen = new Pen(accent, champion != null ? 2f : 1.5f))
                    g.DrawPath(pen, path);
            }

            DrawTrophy(g, new RectangleF(rect.X + 20, rect.Y + 22, 40, 44), champion != null ? Theme.Gold : Theme.TextFaint);

            var textX = rect.X + 74;
            var textWidth = rect.Right - textX - 14;
            using (var caption = new SolidBrush(champion != null ? Theme.Gold : Theme.TextFaint))
                g.DrawString("CHAMPION", ChampionCaptionFont, caption, new RectangleF(textX, rect.Y + 20, textWidth, 18), LeftMiddle);
            using (var name = new SolidBrush(champion != null ? Theme.Text : Theme.TextMuted))
                g.DrawString(champion?.Name ?? "To be decided", ChampionNameFont, name, new RectangleF(textX, rect.Y + 38, textWidth, 34), LeftMiddle);

            if (bracket.RunnerUp != null)
            {
                using (var muted = new SolidBrush(Theme.TextMuted))
                    g.DrawString("Runner-up: " + bracket.RunnerUp.Name, NameFont, muted, new RectangleF(rect.X + 20, rect.Bottom - 40, rect.Width - 34, 24), LeftMiddle);
            }
            else if (champion == null)
            {
                using (var muted = new SolidBrush(Theme.TextFaint))
                    g.DrawString($"{bracket.Entrants.Count} entrants · double elimination", NameFont, muted, new RectangleF(rect.X + 20, rect.Bottom - 40, rect.Width - 34, 24), LeftMiddle);
            }
        }

        /// <summary>A simple trophy: cup, handles, stem and base.</summary>
        private static void DrawTrophy(Graphics g, RectangleF r, Color color)
        {
            using (var brush = new SolidBrush(color))
            using (var pen = new Pen(color, 3f))
            {
                var cupWidth = r.Width * 0.6f;
                var cupX = r.X + (r.Width - cupWidth) / 2;
                using (var cup = new GraphicsPath())
                {
                    cup.AddLine(cupX, r.Y, cupX + cupWidth, r.Y);
                    cup.AddBezier(cupX + cupWidth, r.Y, cupX + cupWidth, r.Y + r.Height * 0.5f, cupX + cupWidth * 0.75f, r.Y + r.Height * 0.55f, cupX + cupWidth / 2, r.Y + r.Height * 0.58f);
                    cup.AddBezier(cupX + cupWidth / 2, r.Y + r.Height * 0.58f, cupX + cupWidth * 0.25f, r.Y + r.Height * 0.55f, cupX, r.Y + r.Height * 0.5f, cupX, r.Y);
                    g.FillPath(brush, cup);
                }

                g.DrawArc(pen, r.X, r.Y + 3, cupWidth * 0.45f, r.Height * 0.3f, 90, 180);
                g.DrawArc(pen, r.Right - cupWidth * 0.45f, r.Y + 3, cupWidth * 0.45f, r.Height * 0.3f, 270, 180);
                g.FillRectangle(brush, r.X + r.Width / 2 - 3, r.Y + r.Height * 0.56f, 6, r.Height * 0.22f);
                using (var basePath = Theme.RoundedRect(new RectangleF(r.X + r.Width * 0.25f, r.Y + r.Height * 0.78f, r.Width * 0.5f, r.Height * 0.2f), 2))
                    g.FillPath(brush, basePath);
            }
        }
    }
}
