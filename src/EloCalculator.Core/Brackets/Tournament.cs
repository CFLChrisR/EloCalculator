using System;
using System.Collections.Generic;
using System.Linq;
using EloCalculator.Core.Players;
using EloCalculator.Core.Rating;

namespace EloCalculator.Core.Brackets
{
    /// <summary>
    /// A named double-elimination bracket. When <see cref="IsRated"/> is set, every
    /// reported result is also recorded as a rated game on the player roster, and
    /// clearing the result undoes that game again.
    /// </summary>
    public sealed class Tournament
    {
        private readonly Dictionary<int, int> ratingRecords = new Dictionary<int, int>();

        public Tournament(string name, IEnumerable<string> namesInSeedOrder, bool isRated)
            : this(name, namesInSeedOrder, isRated, DateTime.Now)
        {
        }

        private Tournament(string name, IEnumerable<string> namesInSeedOrder, bool isRated, DateTime createdAt)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Tournament" : name.Trim();
            IsRated = isRated;
            CreatedAt = createdAt;
            Bracket = new DoubleEliminationBracket(namesInSeedOrder);
        }

        public string Name { get; }

        public bool IsRated { get; }

        public DateTime CreatedAt { get; }

        public DoubleEliminationBracket Bracket { get; }

        public int PlayedCount => Bracket.Matches.Count(m => m.Status == MatchStatus.Completed);

        /// <summary>Matches that still need a result, counting the reset only once it is certain to be played.</summary>
        public int RemainingCount => Bracket.Matches.Count(m =>
            m.Status == MatchStatus.Ready ||
            (m.Status == MatchStatus.Pending && !ReferenceEquals(m, Bracket.GrandFinalReset)));

        public void ReportResult(int matchId, int winnerSlot, PlayerRoster? roster, EloRatingCalculator? calculator)
        {
            Bracket.ReportResult(matchId, winnerSlot);
            if (!IsRated || roster == null || calculator == null)
                return;

            var match = Bracket.GetMatch(matchId);
            var winner = roster.Find(match.Winner!.Name);
            var loser = roster.Find(match.Loser!.Name);
            if (winner != null && loser != null)
                ratingRecords[matchId] = roster.RecordMatch(winner, loser, calculator, $"{Name} · {match.Label}").Id;
        }

        public bool CanClearResult(int matchId) => Bracket.CanClearResult(matchId);

        public void ClearResult(int matchId, PlayerRoster? roster)
        {
            Bracket.ClearResult(matchId);
            if (ratingRecords.TryGetValue(matchId, out var recordId))
            {
                ratingRecords.Remove(matchId);
                roster?.UndoMatch(recordId);
            }
        }

        /// <summary>Clears the most recent result. Returns the match it belonged to, or null if nothing was reported.</summary>
        public BracketMatch? UndoLastResult(PlayerRoster? roster)
        {
            var order = Bracket.ResultOrder;
            if (order.Count == 0)
                return null;

            var matchId = order[order.Count - 1];
            ClearResult(matchId, roster);
            return Bracket.GetMatch(matchId);
        }

        public BracketFile ToFile() => new BracketFile
        {
            Name = Name,
            IsRated = IsRated,
            CreatedAt = CreatedAt,
            Entrants = Bracket.Entrants.Select(e => e.Name).ToList(),
            Results = Bracket.ResultOrder.Select(id => new BracketResult
            {
                MatchId = id,
                WinnerSlot = Bracket.GetMatch(id).WinnerSlot ?? 0,
                RatingRecordId = ratingRecords.TryGetValue(id, out var recordId) ? recordId : 0,
            }).ToList(),
        };

        /// <summary>Rebuilds a tournament by replaying its saved results. Ratings aren't touched.</summary>
        public static Tournament FromFile(BracketFile file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            var tournament = new Tournament(file.Name, file.Entrants, file.IsRated, file.CreatedAt);
            foreach (var result in file.Results)
            {
                tournament.Bracket.ReportResult(result.MatchId, result.WinnerSlot);
                if (result.RatingRecordId != 0)
                    tournament.ratingRecords[result.MatchId] = result.RatingRecordId;
            }

            return tournament;
        }
    }
}
