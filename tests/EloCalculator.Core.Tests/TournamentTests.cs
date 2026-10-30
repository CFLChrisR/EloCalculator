using System.Linq;
using EloCalculator.Core.Brackets;
using EloCalculator.Core.Players;
using EloCalculator.Core.Rating;
using Xunit;

namespace EloCalculator.Core.Tests;

public class TournamentTests
{
    private readonly EloRatingCalculator calculator = new();

    private static PlayerRoster Roster(params string[] names)
    {
        var roster = new PlayerRoster();
        foreach (var name in names)
            roster.Add(name);
        return roster;
    }

    [Fact]
    public void RatedTournamentRecordsAndUndoesGames()
    {
        var roster = Roster("A", "B", "C", "D");
        var tournament = new Tournament("Cup", new[] { "A", "B", "C", "D" }, isRated: true);
        var match = tournament.Bracket.ReadyMatches.First();

        tournament.ReportResult(match.Id, 0, roster, calculator);

        var record = Assert.Single(roster.History);
        Assert.Equal("Cup · W1-1", record.Note);
        Assert.Equal(1016, roster.Find(match.Winner!.Name)!.Rating);

        tournament.UndoLastResult(roster);

        Assert.Empty(roster.History);
        Assert.All(roster.Players, p => Assert.Equal(1000, p.Rating));
    }

    [Fact]
    public void UnratedTournamentLeavesRatingsAlone()
    {
        var roster = Roster("A", "B");
        var tournament = new Tournament("Casual", new[] { "A", "B" }, isRated: false);

        tournament.ReportResult(tournament.Bracket.ReadyMatches.First().Id, 1, roster, calculator);

        Assert.Empty(roster.History);
    }

    [Fact]
    public void FileRoundTripRestoresResults()
    {
        var roster = Roster("A", "B", "C", "D", "E");
        var tournament = new Tournament("Cup", roster.Players.Select(p => p.Name), isRated: true);
        foreach (var match in tournament.Bracket.ReadyMatches.Take(2).ToList())
            tournament.ReportResult(match.Id, 1, roster, calculator);

        var restored = Tournament.FromFile(tournament.ToFile());

        Assert.Equal("Cup", restored.Name);
        Assert.Equal(tournament.Bracket.ResultOrder, restored.Bracket.ResultOrder);
        Assert.Equal(
            tournament.Bracket.Matches.Select(m => m.Status),
            restored.Bracket.Matches.Select(m => m.Status));

        // Undoing after reloading still reverts the rated game.
        restored.UndoLastResult(roster);
        Assert.Single(roster.History);
    }

    [Fact]
    public void RemainingCountExcludesResetUntilItIsNeeded()
    {
        var tournament = new Tournament("Duel", new[] { "A", "B" }, isRated: false);

        // W1-1 and the grand final; the reset only counts once it's certain to be played.
        Assert.Equal(2, tournament.RemainingCount);
    }
}
