using System;
using System.Collections.Generic;
using System.Linq;

namespace EloCalculator.Core.Brackets
{
    /// <summary>
    /// A double-elimination bracket. Players drop to the losers bracket after their
    /// first loss and are eliminated after their second. The winners-bracket champion
    /// meets the losers-bracket champion in the grand final; if the losers-bracket
    /// champion wins, a reset match decides the title.
    /// </summary>
    /// <remarks>
    /// The bracket is sized to the next power of two and padded with byes, which go to
    /// the top seeds. The state of every match is derived from the seeding plus the
    /// reported results, so clearing a result simply recomputes everything downstream.
    /// </remarks>
    public sealed class DoubleEliminationBracket
    {
        private readonly List<BracketMatch> matches = new List<BracketMatch>();
        private readonly Dictionary<int, List<BracketMatch>> dependents = new Dictionary<int, List<BracketMatch>>();
        private readonly Dictionary<int, int> results = new Dictionary<int, int>();
        private readonly List<int> resultOrder = new List<int>();

        /// <param name="namesInSeedOrder">Entrant names, top seed first.</param>
        public DoubleEliminationBracket(IEnumerable<string> namesInSeedOrder)
        {
            if (namesInSeedOrder == null) throw new ArgumentNullException(nameof(namesInSeedOrder));

            var names = namesInSeedOrder.Select(n => (n ?? string.Empty).Trim()).ToList();
            if (names.Count < 2)
                throw new ArgumentException("A bracket needs at least two entrants.", nameof(namesInSeedOrder));
            if (names.Any(n => n.Length == 0))
                throw new ArgumentException("Entrant names can't be blank.", nameof(namesInSeedOrder));
            if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
                throw new ArgumentException("Entrant names must be unique.", nameof(namesInSeedOrder));

            Entrants = names.Select((name, i) => new Entrant(name, i + 1)).ToList();

            Size = 2;
            while (Size < Entrants.Count)
                Size *= 2;
            WinnersRounds = (int)Math.Round(Math.Log(Size, 2));
            LosersRounds = 2 * (WinnersRounds - 1);

            Build();
            Resolve();
        }

        public IReadOnlyList<Entrant> Entrants { get; }

        /// <summary>Number of slots in the first round (a power of two, including byes).</summary>
        public int Size { get; }

        public int WinnersRounds { get; }

        public int LosersRounds { get; }

        /// <summary>All matches in an order where every match comes after the matches feeding it.</summary>
        public IReadOnlyList<BracketMatch> Matches => matches;

        public BracketMatch GrandFinal { get; private set; } = null!;

        public BracketMatch GrandFinalReset { get; private set; } = null!;

        public Entrant? Champion { get; private set; }

        /// <summary>Second place, once the bracket is complete.</summary>
        public Entrant? RunnerUp { get; private set; }

        public bool IsComplete => Champion != null;

        /// <summary>Ids of matches with reported results, oldest first.</summary>
        public IReadOnlyList<int> ResultOrder => resultOrder;

        public IEnumerable<BracketMatch> ReadyMatches => matches.Where(m => m.Status == MatchStatus.Ready);

        public BracketMatch GetMatch(int matchId)
        {
            if (matchId < 1 || matchId > matches.Count)
                throw new ArgumentOutOfRangeException(nameof(matchId), $"There is no match with id {matchId}.");
            return matches[matchId - 1];
        }

        public IEnumerable<BracketMatch> GetRound(BracketSide side, int round) =>
            matches.Where(m => m.Side == side && m.Round == round);

        public string GetRoundName(BracketSide side, int round)
        {
            switch (side)
            {
                case BracketSide.Winners:
                    if (round == WinnersRounds) return "Winners Final";
                    if (round == WinnersRounds - 1 && WinnersRounds >= 3) return "Winners Semifinal";
                    return $"Winners Round {round}";
                case BracketSide.Losers:
                    return round == LosersRounds ? "Losers Final" : $"Losers Round {round}";
                default:
                    return round == 1 ? "Grand Final" : "Grand Final Reset";
            }
        }

        /// <summary>Describes where a slot's player will come from, e.g. "Loser of W2-1".</summary>
        public string DescribeSource(SlotSource source)
        {
            switch (source.Kind)
            {
                case SlotSourceKind.Seed:
                    return source.Value < Entrants.Count ? Entrants[source.Value].Name : "Bye";
                case SlotSourceKind.WinnerOf:
                    return "Winner of " + GetMatch(source.Value).Label;
                default:
                    return "Loser of " + GetMatch(source.Value).Label;
            }
        }

        /// <summary>Matches that take a player from the given match.</summary>
        public IReadOnlyList<BracketMatch> GetDependents(int matchId) =>
            dependents.TryGetValue(matchId, out var list) ? (IReadOnlyList<BracketMatch>)list : Array.Empty<BracketMatch>();

        /// <param name="winnerSlot">0 if <see cref="BracketMatch.EntrantA"/> won, 1 if <see cref="BracketMatch.EntrantB"/> won.</param>
        public void ReportResult(int matchId, int winnerSlot)
        {
            if (winnerSlot != 0 && winnerSlot != 1)
                throw new ArgumentOutOfRangeException(nameof(winnerSlot), "The winner slot must be 0 or 1.");

            var match = GetMatch(matchId);
            if (match.Status != MatchStatus.Ready)
                throw new InvalidOperationException($"Match {match.Label} isn't ready to be reported ({match.Status}).");

            results[matchId] = winnerSlot;
            resultOrder.Add(matchId);
            Resolve();
        }

        public void ReportWinner(int matchId, Entrant winner)
        {
            var match = GetMatch(matchId);
            if (ReferenceEquals(match.EntrantA, winner)) ReportResult(matchId, 0);
            else if (ReferenceEquals(match.EntrantB, winner)) ReportResult(matchId, 1);
            else throw new ArgumentException($"{winner?.Name} isn't playing in match {match.Label}.", nameof(winner));
        }

        /// <summary>
        /// A result can be cleared as long as nothing that depends on it has been reported,
        /// looking through any byes in between.
        /// </summary>
        public bool CanClearResult(int matchId)
        {
            var match = GetMatch(matchId);
            return match.Status == MatchStatus.Completed && results.ContainsKey(matchId) && !HasReportedDownstream(match);
        }

        public void ClearResult(int matchId)
        {
            if (!CanClearResult(matchId))
                throw new InvalidOperationException($"The result of {GetMatch(matchId).Label} can't be cleared while later matches depend on it.");

            results.Remove(matchId);
            resultOrder.Remove(matchId);
            Resolve();
        }

        /// <summary>Clears the most recently reported result. Returns its match id, or null if there was none.</summary>
        public int? UndoLastResult()
        {
            if (resultOrder.Count == 0)
                return null;

            var matchId = resultOrder[resultOrder.Count - 1];
            ClearResult(matchId);
            return matchId;
        }

        private bool HasReportedDownstream(BracketMatch match)
        {
            foreach (var dependent in GetDependents(match.Id))
            {
                if (dependent.Status == MatchStatus.Completed && results.ContainsKey(dependent.Id))
                    return true;
                if (dependent.Status == MatchStatus.Bye && HasReportedDownstream(dependent))
                    return true;
            }

            return false;
        }

        private void Build()
        {
            var winners = new List<List<BracketMatch>>();
            var order = BracketSeeding.GetSeedOrder(Size);

            var firstRound = new List<BracketMatch>();
            for (var i = 0; i < Size / 2; i++)
                firstRound.Add(Add(BracketSide.Winners, 1, i, SlotSource.Seed(order[2 * i] - 1), SlotSource.Seed(order[2 * i + 1] - 1)));
            winners.Add(firstRound);

            for (var r = 2; r <= WinnersRounds; r++)
                winners.Add(PairWinners(BracketSide.Winners, r, winners[r - 2]));

            var winnersFinal = winners[WinnersRounds - 1][0];
            SlotSource losersChampion;

            if (WinnersRounds == 1)
            {
                // Two players: the loser of the only winners match goes straight to the grand final.
                losersChampion = SlotSource.LoserOf(winnersFinal.Id);
            }
            else
            {
                var losers = new List<List<BracketMatch>>();

                // Losers round 1 pairs off everyone who lost in winners round 1.
                var lr1 = new List<BracketMatch>();
                var w1 = winners[0];
                for (var i = 0; i < w1.Count / 2; i++)
                    lr1.Add(Add(BracketSide.Losers, 1, i, SlotSource.LoserOf(w1[2 * i].Id), SlotSource.LoserOf(w1[2 * i + 1].Id)));
                losers.Add(lr1);

                for (var j = 1; j <= WinnersRounds - 1; j++)
                {
                    // Even losers rounds: survivors meet the players dropping from winners round j + 1.
                    // The drop order is varied each round to avoid early rematches.
                    var survivors = losers[losers.Count - 1];
                    var drops = OrderDrops(winners[j], j);
                    var dropRound = new List<BracketMatch>();
                    for (var i = 0; i < survivors.Count; i++)
                        dropRound.Add(Add(BracketSide.Losers, 2 * j, i, SlotSource.WinnerOf(survivors[i].Id), SlotSource.LoserOf(drops[i].Id)));
                    losers.Add(dropRound);

                    // Odd losers rounds: survivors play each other.
                    if (j < WinnersRounds - 1)
                        losers.Add(PairWinners(BracketSide.Losers, 2 * j + 1, dropRound));
                }

                losersChampion = SlotSource.WinnerOf(losers[losers.Count - 1][0].Id);
            }

            GrandFinal = Add(BracketSide.Finals, 1, 0, SlotSource.WinnerOf(winnersFinal.Id), losersChampion);
            GrandFinalReset = Add(BracketSide.Finals, 2, 0, SlotSource.WinnerOf(GrandFinal.Id), SlotSource.LoserOf(GrandFinal.Id));
        }

        private static List<BracketMatch> OrderDrops(List<BracketMatch> round, int j)
        {
            if (round.Count == 1)
                return round;
            if (j % 2 == 1)
                return Enumerable.Reverse(round).ToList();

            var half = round.Count / 2;
            return round.Skip(half).Concat(round.Take(half)).ToList();
        }

        private List<BracketMatch> PairWinners(BracketSide side, int round, List<BracketMatch> previous)
        {
            var next = new List<BracketMatch>();
            for (var i = 0; i < previous.Count / 2; i++)
                next.Add(Add(side, round, i, SlotSource.WinnerOf(previous[2 * i].Id), SlotSource.WinnerOf(previous[2 * i + 1].Id)));
            return next;
        }

        private BracketMatch Add(BracketSide side, int round, int position, SlotSource a, SlotSource b)
        {
            string label;
            switch (side)
            {
                case BracketSide.Winners: label = $"W{round}-{position + 1}"; break;
                case BracketSide.Losers: label = $"L{round}-{position + 1}"; break;
                default: label = round == 1 ? "GF" : "GF Reset"; break;
            }

            var match = new BracketMatch(matches.Count + 1, side, round, position, label, a, b);
            matches.Add(match);
            AddDependent(a, match);
            AddDependent(b, match);
            return match;
        }

        private void AddDependent(SlotSource source, BracketMatch match)
        {
            if (source.Kind == SlotSourceKind.Seed)
                return;
            if (!dependents.TryGetValue(source.Value, out var list))
                dependents[source.Value] = list = new List<BracketMatch>();
            if (!list.Contains(match))
                list.Add(match);
        }

        /// <summary>Recomputes every match from the seeding and the reported results.</summary>
        private void Resolve()
        {
            foreach (var match in matches)
            {
                var (stateA, entrantA) = ResolveSource(match.SourceA);
                var (stateB, entrantB) = ResolveSource(match.SourceB);
                match.StateA = stateA;
                match.EntrantA = entrantA;
                match.StateB = stateB;
                match.EntrantB = entrantB;
                match.Winner = null;
                match.Loser = null;
                match.WinnerSlot = null;

                if (ReferenceEquals(match, GrandFinalReset) && GrandFinal.IsDecided && GrandFinal.WinnerSlot != 1)
                {
                    // The winners-bracket champion won the grand final, so there is no reset.
                    match.Status = MatchStatus.NotNeeded;
                }
                else if (stateA == SlotState.Pending || stateB == SlotState.Pending)
                {
                    match.Status = MatchStatus.Pending;
                }
                else if (stateA == SlotState.Bye || stateB == SlotState.Bye)
                {
                    match.Status = MatchStatus.Bye;
                    match.Winner = entrantA ?? entrantB;
                }
                else if (results.TryGetValue(match.Id, out var slot))
                {
                    match.Status = MatchStatus.Completed;
                    match.WinnerSlot = slot;
                    match.Winner = slot == 0 ? entrantA : entrantB;
                    match.Loser = slot == 0 ? entrantB : entrantA;
                }
                else
                {
                    match.Status = MatchStatus.Ready;
                }
            }

            Champion = null;
            RunnerUp = null;
            if (GrandFinal.IsDecided && GrandFinalReset.Status == MatchStatus.NotNeeded)
            {
                Champion = GrandFinal.Winner;
                RunnerUp = GrandFinal.Loser;
            }
            else if (GrandFinalReset.IsDecided)
            {
                Champion = GrandFinalReset.Winner;
                RunnerUp = GrandFinalReset.Loser;
            }
        }

        private (SlotState, Entrant?) ResolveSource(SlotSource source)
        {
            switch (source.Kind)
            {
                case SlotSourceKind.Seed:
                    return source.Value < Entrants.Count
                        ? (SlotState.Filled, Entrants[source.Value])
                        : (SlotState.Bye, (Entrant?)null);

                case SlotSourceKind.WinnerOf:
                {
                    var feeder = GetMatch(source.Value);
                    if (!feeder.IsDecided) return (SlotState.Pending, null);
                    return feeder.Winner == null ? (SlotState.Bye, (Entrant?)null) : (SlotState.Filled, feeder.Winner);
                }

                default:
                {
                    var feeder = GetMatch(source.Value);
                    if (feeder.Status == MatchStatus.Bye) return (SlotState.Bye, null);
                    if (feeder.Status != MatchStatus.Completed) return (SlotState.Pending, null);
                    return (SlotState.Filled, feeder.Loser);
                }
            }
        }
    }
}
