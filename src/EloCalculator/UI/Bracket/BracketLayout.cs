using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using EloCalculator.Core.Brackets;

namespace EloCalculator.UI.Bracket
{
    internal sealed class BracketSection
    {
        public BracketSection(BracketSide side, string title, RectangleF bounds)
        {
            Side = side;
            Title = title;
            Bounds = bounds;
        }

        public BracketSide Side { get; }

        public string Title { get; }

        public RectangleF Bounds { get; }
    }

    internal sealed class RoundHeader
    {
        public RoundHeader(string text, RectangleF bounds)
        {
            Text = text;
            Bounds = bounds;
        }

        public string Text { get; }

        public RectangleF Bounds { get; }
    }

    /// <summary>
    /// Positions every match card in "world" coordinates. Each round is a column, and every
    /// match sits midway between the two matches that feed it so connector lines stay tidy.
    /// The winners bracket is on top, the losers bracket below, and the finals to the right.
    /// </summary>
    internal sealed class BracketLayout
    {
        public const float CardWidth = 220;
        public const float RowHeight = 30;
        public const float CardHeight = RowHeight * 2;
        public const float ColumnGap = 72;
        public const float LabelHeight = 18;
        public const float VerticalGap = 22;
        public const float Margin = 40;
        public const float SectionPadding = 24;
        public const float SectionTitleHeight = 40;
        public const float RoundHeaderHeight = 34;
        public const float SectionGap = 48;
        public const float Pitch = CardHeight + LabelHeight + VerticalGap;

        private readonly Dictionary<int, RectangleF> cards = new Dictionary<int, RectangleF>();
        private readonly List<BracketSection> sections = new List<BracketSection>();
        private readonly List<RoundHeader> headers = new List<RoundHeader>();

        private BracketLayout()
        {
        }

        public IReadOnlyList<BracketSection> Sections => sections;

        public IReadOnlyList<RoundHeader> Headers => headers;

        public RectangleF ChampionBounds { get; private set; }

        public SizeF Size { get; private set; }

        public RectangleF GetCard(int matchId) => cards[matchId];

        public static BracketLayout Build(DoubleEliminationBracket bracket)
        {
            var layout = new BracketLayout();
            var winnersBottom = layout.LayoutSide(bracket, BracketSide.Winners, bracket.WinnersRounds, Margin, "WINNERS BRACKET");
            var bottom = winnersBottom;
            if (bracket.LosersRounds > 0)
                bottom = layout.LayoutSide(bracket, BracketSide.Losers, bracket.LosersRounds, winnersBottom + SectionGap, "LOSERS BRACKET");

            layout.LayoutFinals(bracket, Math.Max(bracket.WinnersRounds, bracket.LosersRounds));

            var right = layout.sections.Max(s => s.Bounds.Right);
            bottom = Math.Max(bottom, layout.sections.Max(s => s.Bounds.Bottom));
            layout.Size = new SizeF(right + Margin, bottom + Margin);
            return layout;
        }

        private static float ColumnX(int column) => Margin + SectionPadding + column * (CardWidth + ColumnGap);

        /// <summary>Lays out one side of the bracket starting at <paramref name="top"/> and returns its bottom edge.</summary>
        private float LayoutSide(DoubleEliminationBracket bracket, BracketSide side, int rounds, float top, string title)
        {
            var headerTop = top + SectionTitleHeight;
            var firstCardTop = headerTop + RoundHeaderHeight + LabelHeight;
            var centers = new Dictionary<int, float>();
            var bottom = firstCardTop;

            for (var round = 1; round <= rounds; round++)
            {
                var x = ColumnX(round - 1);
                headers.Add(new RoundHeader(bracket.GetRoundName(side, round), new RectangleF(x, headerTop, CardWidth, RoundHeaderHeight - 8)));

                foreach (var match in bracket.GetRound(side, round))
                {
                    float center;
                    var a = FeederCenter(match.SourceA, side, centers);
                    var b = FeederCenter(match.SourceB, side, centers);
                    if (a.HasValue && b.HasValue) center = (a.Value + b.Value) / 2;
                    else if (a.HasValue) center = a.Value;
                    else if (b.HasValue) center = b.Value;
                    else center = firstCardTop + match.Position * Pitch + CardHeight / 2;

                    centers[match.Id] = center;
                    var rect = new RectangleF(x, center - CardHeight / 2, CardWidth, CardHeight);
                    cards[match.Id] = rect;
                    bottom = Math.Max(bottom, rect.Bottom);
                }
            }

            var width = rounds * (CardWidth + ColumnGap) - ColumnGap + SectionPadding * 2;
            var bounds = new RectangleF(Margin, top, width, bottom + SectionPadding - top);
            sections.Add(new BracketSection(side, title, bounds));
            return bounds.Bottom;
        }

        /// <summary>The centre of the same-side match feeding a slot, if any.</summary>
        private static float? FeederCenter(SlotSource source, BracketSide side, Dictionary<int, float> centers)
        {
            if (source.Kind != SlotSourceKind.WinnerOf)
                return null;
            return centers.TryGetValue(source.Value, out var center) ? center : (float?)null;
        }

        private void LayoutFinals(DoubleEliminationBracket bracket, int column)
        {
            var winnersFinal = cards[bracket.GrandFinal.SourceA.Value];
            var center = winnersFinal.Top + CardHeight / 2;
            if (bracket.GrandFinal.SourceB.Kind == SlotSourceKind.WinnerOf)
            {
                var losersFinal = cards[bracket.GrandFinal.SourceB.Value];
                center = (center + losersFinal.Top + CardHeight / 2) / 2;
            }

            var top = center - CardHeight / 2;
            var x = ColumnX(column);
            cards[bracket.GrandFinal.Id] = new RectangleF(x, top, CardWidth, CardHeight);
            cards[bracket.GrandFinalReset.Id] = new RectangleF(x + CardWidth + ColumnGap, top, CardWidth, CardHeight);
            headers.Add(new RoundHeader("Grand Final", new RectangleF(x, top - LabelHeight - RoundHeaderHeight, CardWidth, RoundHeaderHeight - 8)));
            headers.Add(new RoundHeader("Reset (if needed)", new RectangleF(x + CardWidth + ColumnGap, top - LabelHeight - RoundHeaderHeight, CardWidth, RoundHeaderHeight - 8)));

            var championX = x + 2 * (CardWidth + ColumnGap);
            ChampionBounds = new RectangleF(championX, center - 64, 250, 128);

            var sectionTop = top - LabelHeight - RoundHeaderHeight - SectionTitleHeight;
            var sectionLeft = x - SectionPadding;
            var bounds = RectangleF.FromLTRB(sectionLeft, sectionTop, ChampionBounds.Right + SectionPadding, ChampionBounds.Bottom + SectionPadding);
            sections.Add(new BracketSection(BracketSide.Finals, "FINALS", bounds));
        }

        /// <summary>Finds the match card (and slot: 0 top, 1 bottom) under a world-space point.</summary>
        public bool HitTest(PointF point, out int matchId, out int slot)
        {
            foreach (var pair in cards)
            {
                if (pair.Value.Contains(point))
                {
                    matchId = pair.Key;
                    slot = point.Y < pair.Value.Top + RowHeight ? 0 : 1;
                    return true;
                }
            }

            matchId = 0;
            slot = 0;
            return false;
        }
    }
}
