using System;
using System.IO;
using EloCalculator.Core.Players;
using EloCalculator.Core.Rating;
using EloCalculator.Core.Storage;
using Xunit;

namespace EloCalculator.Core.Tests;

public class PlayerRosterTests
{
    private readonly EloRatingCalculator calculator = new();

    [Fact]
    public void AddTrimsNamesAndRejectsBlanksAndDuplicates()
    {
        var roster = new PlayerRoster();
        Assert.Equal("Alice", roster.Add("  Alice ").Name);
        Assert.Throws<ArgumentException>(() => roster.Add("   "));
        Assert.Throws<ArgumentException>(() => roster.Add("alice"));
    }

    [Fact]
    public void RecordMatchUpdatesBothPlayers()
    {
        var roster = new PlayerRoster();
        var alice = roster.Add("Alice");
        var bob = roster.Add("Bob");

        var record = roster.RecordMatch(alice, bob, calculator);

        Assert.Equal(16, record.RatingChange);
        Assert.Equal(1016, alice.Rating);
        Assert.Equal(984, bob.Rating);
        Assert.Equal(1, alice.Wins);
        Assert.Equal(1, bob.Losses);
        Assert.Equal(1.0, alice.WinRate);
        Assert.Equal(0.0, bob.WinRate);
        Assert.Equal(1000, alice.AverageOpponentRating);
    }

    [Fact]
    public void UndoMatchRestoresStatsEvenWhenNotTheLatest()
    {
        var roster = new PlayerRoster();
        var alice = roster.Add("Alice");
        var bob = roster.Add("Bob");
        var carol = roster.Add("Carol");

        var first = roster.RecordMatch(alice, bob, calculator);
        var second = roster.RecordMatch(carol, alice, calculator);

        Assert.True(roster.UndoMatch(first.Id));

        Assert.Equal(1000, bob.Rating);
        Assert.Equal(0, bob.GamesPlayed);
        Assert.Equal(1000 - second.RatingChange, alice.Rating);
        Assert.Equal(1, alice.Losses);
        Assert.Equal(0, alice.Wins);
        Assert.Single(roster.History);
        Assert.False(roster.UndoMatch(first.Id));
    }

    [Fact]
    public void StandingsAreSortedByRating()
    {
        var roster = new PlayerRoster();
        var alice = roster.Add("Alice");
        var bob = roster.Add("Bob");
        roster.RecordMatch(bob, alice, calculator);

        Assert.Equal(new[] { bob, alice }, roster.Standings);
    }

    [Fact]
    public void LoadsFilesSavedByTheOriginalApp()
    {
        var roster = XmlStore.Load<PlayerRoster>(Path.Combine("TestData", "legacy-players.xml"));

        var player = Assert.Single(roster.Players);
        Assert.Equal("ee", player.Name);
        Assert.Equal(1000, player.Rating);
        Assert.Empty(roster.History);
    }

    [Fact]
    public void SaveAndLoadRoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xml");
        try
        {
            var roster = new PlayerRoster();
            roster.RecordMatch(roster.Add("Alice"), roster.Add("Bob"), calculator, "Friendly");
            XmlStore.Save(path, roster);
            XmlStore.Save(path, roster); // overwriting an existing file

            var loaded = XmlStore.Load<PlayerRoster>(path);

            Assert.Equal(1016, loaded.Find("Alice")!.Rating);
            Assert.Equal("Friendly", Assert.Single(loaded.History).Note);
            Assert.Contains("<peopleList>", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
