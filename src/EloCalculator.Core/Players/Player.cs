using System;
using System.Xml.Serialization;

namespace EloCalculator.Core.Players
{
    /// <summary>
    /// A rated player. The XML element names match the original file format so
    /// player lists saved by earlier versions of the app still load.
    /// </summary>
    public sealed class Player
    {
        public const int DefaultRating = 1000;

        public Player() : this(string.Empty)
        {
        }

        public Player(string name, int rating = DefaultRating)
        {
            Name = name;
            Rating = rating;
        }

        [XmlElement("name")]
        public string Name { get; set; }

        [XmlElement("MMR")]
        public int Rating { get; set; }

        [XmlElement("BasePlayerWins")]
        public int Wins { get; set; }

        [XmlElement("BasePlayerLosses")]
        public int Losses { get; set; }

        [XmlElement("TotalofAllOpponentRatings")]
        public long TotalOpponentRating { get; set; }

        [XmlIgnore]
        public int GamesPlayed => Wins + Losses;

        /// <summary>Fraction of games won, between 0 and 1.</summary>
        [XmlIgnore]
        public double WinRate => GamesPlayed == 0 ? 0 : (double)Wins / GamesPlayed;

        [XmlIgnore]
        public int AverageOpponentRating =>
            GamesPlayed == 0 ? 0 : (int)Math.Round((double)TotalOpponentRating / GamesPlayed);

        public override string ToString() => Name;
    }
}
