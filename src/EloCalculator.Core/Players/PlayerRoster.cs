using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using EloCalculator.Core.Rating;

namespace EloCalculator.Core.Players
{
    /// <summary>
    /// The list of players plus the history of recorded games. Serialized with the
    /// original <c>peopleList</c>/<c>peeps</c> element names for backwards compatibility.
    /// </summary>
    [XmlRoot("peopleList")]
    public sealed class PlayerRoster
    {
        [XmlArray("peeps")]
        [XmlArrayItem("BasePlayerClass")]
        public List<Player> Players { get; set; } = new List<Player>();

        [XmlArray("history")]
        [XmlArrayItem("match")]
        public List<MatchRecord> History { get; set; } = new List<MatchRecord>();

        /// <summary>Players ordered by rating, best first.</summary>
        [XmlIgnore]
        public IEnumerable<Player> Standings =>
            Players.OrderByDescending(p => p.Rating)
                   .ThenByDescending(p => p.Wins)
                   .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase);

        public Player? Find(string name) =>
            Players.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        public Player Add(string name)
        {
            var trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                throw new ArgumentException("A player name can't be blank.", nameof(name));
            if (Find(trimmed) != null)
                throw new ArgumentException($"A player named \"{trimmed}\" already exists.", nameof(name));

            var player = new Player(trimmed);
            Players.Add(player);
            return player;
        }

        public bool Remove(string name)
        {
            var player = Find(name);
            return player != null && Players.Remove(player);
        }

        public void Clear()
        {
            Players.Clear();
            History.Clear();
        }

        public MatchRecord RecordMatch(Player winner, Player loser, EloRatingCalculator calculator, string? note = null)
        {
            if (winner == null) throw new ArgumentNullException(nameof(winner));
            if (loser == null) throw new ArgumentNullException(nameof(loser));
            if (calculator == null) throw new ArgumentNullException(nameof(calculator));
            if (ReferenceEquals(winner, loser))
                throw new ArgumentException("A player can't play against themselves.");

            var change = calculator.RatingChange(winner.Rating, loser.Rating);
            var record = new MatchRecord
            {
                Id = History.Count == 0 ? 1 : History.Max(h => h.Id) + 1,
                PlayedAt = DateTime.Now,
                Winner = winner.Name,
                Loser = loser.Name,
                WinnerRatingBefore = winner.Rating,
                LoserRatingBefore = loser.Rating,
                RatingChange = change,
                Note = note,
            };

            winner.Wins++;
            winner.TotalOpponentRating += loser.Rating;
            winner.Rating += change;

            loser.Losses++;
            loser.TotalOpponentRating += record.WinnerRatingBefore;
            loser.Rating -= change;

            History.Add(record);
            return record;
        }

        /// <summary>
        /// Reverses a recorded game. Players that were removed since are skipped.
        /// Returns false when no record has the given id.
        /// </summary>
        public bool UndoMatch(int recordId)
        {
            var record = History.FirstOrDefault(h => h.Id == recordId);
            if (record == null)
                return false;

            var winner = Find(record.Winner);
            if (winner != null)
            {
                winner.Wins = Math.Max(0, winner.Wins - 1);
                winner.TotalOpponentRating -= record.LoserRatingBefore;
                winner.Rating -= record.RatingChange;
            }

            var loser = Find(record.Loser);
            if (loser != null)
            {
                loser.Losses = Math.Max(0, loser.Losses - 1);
                loser.TotalOpponentRating -= record.WinnerRatingBefore;
                loser.Rating += record.RatingChange;
            }

            History.Remove(record);
            return true;
        }
    }
}
