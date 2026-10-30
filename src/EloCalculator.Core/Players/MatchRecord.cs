using System;
using System.Xml.Serialization;

namespace EloCalculator.Core.Players
{
    /// <summary>
    /// One recorded game. Stores the ratings before the game and the rating change
    /// so the game can be undone later without disturbing other results.
    /// </summary>
    public sealed class MatchRecord
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        [XmlAttribute("playedAt")]
        public DateTime PlayedAt { get; set; }

        [XmlAttribute("winner")]
        public string Winner { get; set; } = string.Empty;

        [XmlAttribute("loser")]
        public string Loser { get; set; } = string.Empty;

        [XmlAttribute("winnerRating")]
        public int WinnerRatingBefore { get; set; }

        [XmlAttribute("loserRating")]
        public int LoserRatingBefore { get; set; }

        /// <summary>Points gained by the winner and lost by the loser.</summary>
        [XmlAttribute("change")]
        public int RatingChange { get; set; }

        [XmlAttribute("note")]
        public string? Note { get; set; }
    }
}
