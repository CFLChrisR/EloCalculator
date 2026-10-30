namespace EloCalculator.Core.Brackets
{
    /// <summary>A competitor in a bracket. Seed 1 is the top seed.</summary>
    public sealed class Entrant
    {
        public Entrant(string name, int seed)
        {
            Name = name;
            Seed = seed;
        }

        public string Name { get; }

        public int Seed { get; }

        public override string ToString() => $"({Seed}) {Name}";
    }
}
