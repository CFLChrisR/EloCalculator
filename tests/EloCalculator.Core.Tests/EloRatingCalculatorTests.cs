using EloCalculator.Core.Rating;
using Xunit;

namespace EloCalculator.Core.Tests;

public class EloRatingCalculatorTests
{
    [Fact]
    public void EvenlyMatchedPlayersAreFiftyFifty()
    {
        Assert.Equal(0.5, EloRatingCalculator.ExpectedScore(1200, 1200), 6);
    }

    [Fact]
    public void FourHundredPointGapIsTenToOne()
    {
        Assert.Equal(10.0 / 11.0, EloRatingCalculator.ExpectedScore(1400, 1000), 6);
    }

    [Theory]
    [InlineData(1000, 1000, 16)]
    [InlineData(1400, 1000, 3)]
    [InlineData(1000, 1400, 29)]
    public void RatingChangeUsesKFactor(int winner, int loser, int expected)
    {
        Assert.Equal(expected, new EloRatingCalculator(32).RatingChange(winner, loser));
    }

    [Fact]
    public void AWinIsAlwaysWorthAtLeastOnePoint()
    {
        Assert.Equal(1, new EloRatingCalculator(32).RatingChange(3000, 1000));
    }
}
