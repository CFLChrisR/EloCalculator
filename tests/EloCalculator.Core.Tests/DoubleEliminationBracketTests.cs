using System;
using System.Collections.Generic;
using System.Linq;
using EloCalculator.Core.Brackets;
using Xunit;

namespace EloCalculator.Core.Tests;

public class DoubleEliminationBracketTests
{
    private static List<string> Names(int count) => Enumerable.Range(1, count).Select(i => $"P{i}").ToList();

    [Fact]
    public void SeedOrderKeepsTopSeedsApart()
    {
        Assert.Equal(new[] { 1, 8, 4, 5, 2, 7, 3, 6 }, BracketSeeding.GetSeedOrder(8));
    }

    [Theory]
    [InlineData(2, 2, 1, 0)]
    [InlineData(3, 4, 2, 2)]
    [InlineData(8, 8, 3, 4)]
    [InlineData(9, 16, 4, 6)]
    public void BracketIsSizedToNextPowerOfTwo(int entrants, int size, int winnersRounds, int losersRounds)
    {
        var bracket = new DoubleEliminationBracket(Names(entrants));

        Assert.Equal(size, bracket.Size);
        Assert.Equal(winnersRounds, bracket.WinnersRounds);
        Assert.Equal(losersRounds, bracket.LosersRounds);
        // (size - 1) winners matches, (size - 2) losers matches, grand final and reset.
        Assert.Equal(2 * size - 1, bracket.Matches.Count);
    }

    [Fact]
    public void ByesGoToTopSeeds()
    {
        var bracket = new DoubleEliminationBracket(Names(6));
        var firstRound = bracket.GetRound(BracketSide.Winners, 1).ToList();

        var byes = firstRound.Where(m => m.Status == MatchStatus.Bye).Select(m => m.Winner!.Seed).OrderBy(s => s);
        Assert.Equal(new[] { 1, 2 }, byes);
        Assert.Equal(2, bracket.ReadyMatches.Count());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    public void EveryBracketFinishesWithEachLoserEliminatedTwice(int randomSeed)
    {
        var random = new Random(randomSeed);
        for (var count = 2; count <= 40; count++)
        {
            var bracket = new DoubleEliminationBracket(Names(count));
            var losses = bracket.Entrants.ToDictionary(e => e, _ => 0);

            while (bracket.ReadyMatches.FirstOrDefault() is { } match)
            {
                Assert.True(losses[match.EntrantA!] < 2 && losses[match.EntrantB!] < 2, "An eliminated player was scheduled again.");
                bracket.ReportResult(match.Id, random.Next(2));
                losses[match.Loser!]++;
            }

            Assert.True(bracket.IsComplete, $"Bracket of {count} didn't finish.");
            Assert.True(losses[bracket.Champion!] <= 1);
            Assert.All(losses.Where(kv => kv.Key != bracket.Champion), kv => Assert.Equal(2, kv.Value));

            var played = bracket.Matches.Count(m => m.Status == MatchStatus.Completed);
            Assert.InRange(played, 2 * count - 2, 2 * count - 1);
        }
    }

    [Fact]
    public void ResetIsSkippedWhenWinnersChampionWinsGrandFinal()
    {
        var bracket = PlayToGrandFinal(4);

        bracket.ReportResult(bracket.GrandFinal.Id, 0);

        Assert.Equal(MatchStatus.NotNeeded, bracket.GrandFinalReset.Status);
        Assert.Equal("P1", bracket.Champion!.Name);
        Assert.NotNull(bracket.RunnerUp);
    }

    [Fact]
    public void ResetIsPlayedWhenLosersChampionWinsGrandFinal()
    {
        var bracket = PlayToGrandFinal(4);
        var losersChampion = bracket.GrandFinal.EntrantB!;

        bracket.ReportResult(bracket.GrandFinal.Id, 1);

        Assert.Equal(MatchStatus.Ready, bracket.GrandFinalReset.Status);
        Assert.False(bracket.IsComplete);

        bracket.ReportWinner(bracket.GrandFinalReset.Id, losersChampion);
        Assert.Same(losersChampion, bracket.Champion);
    }

    [Fact]
    public void ResultsCanOnlyBeClearedBeforeLaterMatchesArePlayed()
    {
        var bracket = new DoubleEliminationBracket(Names(4));
        var w1 = bracket.GetRound(BracketSide.Winners, 1).ToList();
        bracket.ReportResult(w1[0].Id, 0);
        bracket.ReportResult(w1[1].Id, 0);

        Assert.True(bracket.CanClearResult(w1[0].Id));

        var losersRound1 = bracket.GetRound(BracketSide.Losers, 1).Single();
        bracket.ReportResult(losersRound1.Id, 0);

        Assert.False(bracket.CanClearResult(w1[0].Id));
        Assert.Throws<InvalidOperationException>(() => bracket.ClearResult(w1[0].Id));

        Assert.Equal(losersRound1.Id, bracket.UndoLastResult());
        Assert.True(bracket.CanClearResult(w1[0].Id));
        bracket.ClearResult(w1[0].Id);

        Assert.Equal(MatchStatus.Ready, w1[0].Status);
        Assert.Equal(MatchStatus.Pending, losersRound1.Status);
    }

    [Fact]
    public void ClearingLooksThroughByes()
    {
        // With 3 entrants, the loser of W1-2 meets a bye in the losers bracket and
        // advances automatically; clearing W1-2 must be blocked once that player has played on.
        var bracket = new DoubleEliminationBracket(Names(3));
        var played = bracket.ReadyMatches.Single();
        bracket.ReportResult(played.Id, 0);

        var winnersFinal = bracket.GetRound(BracketSide.Winners, 2).Single();
        bracket.ReportResult(winnersFinal.Id, 0);

        var losersFinal = bracket.GetRound(BracketSide.Losers, 2).Single();
        Assert.Equal(MatchStatus.Ready, losersFinal.Status);
        bracket.ReportResult(losersFinal.Id, 0);

        Assert.False(bracket.CanClearResult(played.Id));
    }

    [Fact]
    public void ReportingANonReadyMatchThrows()
    {
        var bracket = new DoubleEliminationBracket(Names(4));
        Assert.Throws<InvalidOperationException>(() => bracket.ReportResult(bracket.GrandFinal.Id, 0));
    }

    [Fact]
    public void RejectsInvalidEntrants()
    {
        Assert.Throws<ArgumentException>(() => new DoubleEliminationBracket(new[] { "Solo" }));
        Assert.Throws<ArgumentException>(() => new DoubleEliminationBracket(new[] { "A", "a" }));
        Assert.Throws<ArgumentException>(() => new DoubleEliminationBracket(new[] { "A", " " }));
    }

    [Fact]
    public void DescribesWhereLosersBracketPlayersComeFrom()
    {
        var bracket = new DoubleEliminationBracket(Names(8));
        var losersRound2 = bracket.GetRound(BracketSide.Losers, 2).First();

        Assert.StartsWith("Winner of L1-", bracket.DescribeSource(losersRound2.SourceA));
        Assert.StartsWith("Loser of W2-", bracket.DescribeSource(losersRound2.SourceB));
        Assert.Equal("Winners Semifinal", bracket.GetRoundName(BracketSide.Winners, 2));
        Assert.Equal("Losers Final", bracket.GetRoundName(BracketSide.Losers, 4));
    }

    /// <summary>Plays every match before the grand final, with the higher seed always winning.</summary>
    private static DoubleEliminationBracket PlayToGrandFinal(int count)
    {
        var bracket = new DoubleEliminationBracket(Names(count));
        while (bracket.ReadyMatches.FirstOrDefault(m => m.Side != BracketSide.Finals) is { } match)
            bracket.ReportResult(match.Id, match.EntrantA!.Seed < match.EntrantB!.Seed ? 0 : 1);
        Assert.Equal(MatchStatus.Ready, bracket.GrandFinal.Status);
        return bracket;
    }
}
