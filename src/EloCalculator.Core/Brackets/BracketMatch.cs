namespace EloCalculator.Core.Brackets
{
    public enum BracketSide
    {
        Winners,
        Losers,
        Finals,
    }

    public enum MatchStatus
    {
        /// <summary>At least one slot is waiting on an earlier match.</summary>
        Pending,

        /// <summary>Both players are known and the result can be reported.</summary>
        Ready,

        Completed,

        /// <summary>One or both slots are empty, so the match resolved itself.</summary>
        Bye,

        /// <summary>The grand final reset wasn't needed because the winners-bracket champion won.</summary>
        NotNeeded,
    }

    public enum SlotState
    {
        Pending,
        Filled,
        Bye,
    }

    public enum SlotSourceKind
    {
        Seed,
        WinnerOf,
        LoserOf,
    }

    /// <summary>Where a match slot gets its player from.</summary>
    public readonly struct SlotSource
    {
        private SlotSource(SlotSourceKind kind, int value)
        {
            Kind = kind;
            Value = value;
        }

        public SlotSourceKind Kind { get; }

        /// <summary>Zero-based seed index for <see cref="SlotSourceKind.Seed"/>, otherwise a match id.</summary>
        public int Value { get; }

        public static SlotSource Seed(int index) => new SlotSource(SlotSourceKind.Seed, index);

        public static SlotSource WinnerOf(int matchId) => new SlotSource(SlotSourceKind.WinnerOf, matchId);

        public static SlotSource LoserOf(int matchId) => new SlotSource(SlotSourceKind.LoserOf, matchId);
    }

    public sealed class BracketMatch
    {
        internal BracketMatch(int id, BracketSide side, int round, int position, string label, SlotSource sourceA, SlotSource sourceB)
        {
            Id = id;
            Side = side;
            Round = round;
            Position = position;
            Label = label;
            SourceA = sourceA;
            SourceB = sourceB;
        }

        public int Id { get; }

        public BracketSide Side { get; }

        /// <summary>One-based round number within <see cref="Side"/>.</summary>
        public int Round { get; }

        /// <summary>Zero-based position of the match within its round, top to bottom.</summary>
        public int Position { get; }

        /// <summary>Short display label such as "W1-3", "L2-1" or "GF".</summary>
        public string Label { get; }

        public SlotSource SourceA { get; }

        public SlotSource SourceB { get; }

        public SlotState StateA { get; internal set; }

        public SlotState StateB { get; internal set; }

        public Entrant? EntrantA { get; internal set; }

        public Entrant? EntrantB { get; internal set; }

        public MatchStatus Status { get; internal set; }

        public Entrant? Winner { get; internal set; }

        public Entrant? Loser { get; internal set; }

        /// <summary>0 when <see cref="EntrantA"/> won, 1 when <see cref="EntrantB"/> won; null if not reported.</summary>
        public int? WinnerSlot { get; internal set; }

        /// <summary>True once the match has a winner, whether played or a bye.</summary>
        public bool IsDecided => Status == MatchStatus.Completed || Status == MatchStatus.Bye;

        public Entrant? GetEntrant(int slot) => slot == 0 ? EntrantA : EntrantB;

        public SlotState GetState(int slot) => slot == 0 ? StateA : StateB;

        public SlotSource GetSource(int slot) => slot == 0 ? SourceA : SourceB;

        public bool Involves(Entrant? entrant) =>
            entrant != null && (ReferenceEquals(EntrantA, entrant) || ReferenceEquals(EntrantB, entrant));

        public override string ToString() => $"{Label}: {EntrantA?.Name ?? "?"} vs {EntrantB?.Name ?? "?"} [{Status}]";
    }
}
