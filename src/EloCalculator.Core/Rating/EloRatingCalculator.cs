using System;

namespace EloCalculator.Core.Rating
{
    /// <summary>Standard Elo rating maths.</summary>
    public sealed class EloRatingCalculator
    {
        public const int DefaultKFactor = 32;

        public EloRatingCalculator(int kFactor = DefaultKFactor)
        {
            if (kFactor <= 0)
                throw new ArgumentOutOfRangeException(nameof(kFactor), "The K-factor must be positive.");
            KFactor = kFactor;
        }

        /// <summary>The largest number of points a single game can move a rating.</summary>
        public int KFactor { get; }

        /// <summary>Probability (0 to 1) that a player rated <paramref name="rating"/> beats the opponent.</summary>
        public static double ExpectedScore(int rating, int opponentRating) =>
            1.0 / (1.0 + Math.Pow(10, (opponentRating - rating) / 400.0));

        /// <summary>
        /// Points the winner gains and the loser loses. Always at least one point,
        /// so a win is never worth nothing.
        /// </summary>
        public int RatingChange(int winnerRating, int loserRating)
        {
            var change = KFactor * (1 - ExpectedScore(winnerRating, loserRating));
            return Math.Max(1, (int)Math.Round(change, MidpointRounding.AwayFromZero));
        }
    }
}
