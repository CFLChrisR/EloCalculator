using System.Text.Json;

namespace ThreePLAudit.Core.Regulations;

public sealed record CoverageCell(string Jurisdiction, string Topic, int Entries, int Verified);

/// <summary>
/// In-memory index over the reference library. Loaded from JSON files (the seeded library ships
/// in <c>data/regulations</c>) or from the database.
/// </summary>
public sealed class RegulationLibrary
{
    private readonly Dictionary<string, RegulationReference> _byId = new(StringComparer.OrdinalIgnoreCase);

    public RegulationLibrary(IEnumerable<RegulationReference>? references = null)
    {
        foreach (var r in references ?? []) Upsert(r);
    }

    public IReadOnlyCollection<RegulationReference> All => _byId.Values;

    public RegulationReference? Get(string id) => _byId.GetValueOrDefault(id);

    public void Upsert(RegulationReference reference) =>
        _byId[reference.Id] = reference with { Jurisdiction = reference.Jurisdiction.ToUpperInvariant() };

    public bool Remove(string id) => _byId.Remove(id);

    /// <summary>
    /// Everything that applies in <paramref name="jurisdiction"/>. For a state this also returns
    /// federal law and model/industry entries by default, since all of them apply there.
    /// </summary>
    public IReadOnlyList<RegulationReference> Query(
        string? jurisdiction = null, string? topic = null, string? text = null,
        bool includeFederal = true, bool includeModel = true)
    {
        IEnumerable<RegulationReference> q = _byId.Values;

        if (!string.IsNullOrWhiteSpace(jurisdiction))
        {
            var j = jurisdiction.ToUpperInvariant();
            q = q.Where(r => r.Jurisdiction == j
                             || (includeFederal && r.Jurisdiction == Jurisdictions.Federal)
                             || (includeModel && r.Jurisdiction == Jurisdictions.ModelOrIndustry));
        }
        if (!string.IsNullOrWhiteSpace(topic))
            q = q.Where(r => r.Topics.Contains(topic, StringComparer.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(text))
            q = q.Where(r => Matches(r, text));

        return q.OrderBy(r => JurisdictionRank(r.Jurisdiction, jurisdiction))
                .ThenBy(r => r.Jurisdiction)
                .ThenBy(r => r.Citation, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    public IEnumerable<RegulationReference> WithRequirements() => _byId.Values.Where(r => r.Requirement is not null);

    /// <summary>State-by-topic matrix showing where the library is thin, so gaps are visible.</summary>
    public IReadOnlyList<CoverageCell> Coverage(IEnumerable<string>? topics = null)
    {
        var topicList = (topics ?? Topics.StateCoverage).ToList();
        var cells = new List<CoverageCell>();
        foreach (var state in Jurisdictions.States.Keys)
        foreach (var topic in topicList)
        {
            var entries = _byId.Values.Where(r => r.Jurisdiction == state && r.Topics.Contains(topic)).ToList();
            cells.Add(new(state, topic, entries.Count,
                entries.Count(e => e.Status is VerificationStatus.HumanVerified)));
        }
        return cells;
    }

    private static bool Matches(RegulationReference r, string text) =>
        new[] { r.Id, r.Title, r.Citation, r.Summary, r.Notes ?? "" }.Concat(r.KeyPoints)
            .Any(s => s.Contains(text, StringComparison.OrdinalIgnoreCase));

    private static int JurisdictionRank(string j, string? requested) =>
        requested is not null && j.Equals(requested, StringComparison.OrdinalIgnoreCase) ? 0
        : j == Jurisdictions.Federal ? 1
        : j == Jurisdictions.ModelOrIndustry ? 2
        : 3;

    // ------------------------------------------------------------ JSON persistence

    public static readonly JsonSerializerOptions JsonOptions = AuditJson.Options;

    public static RegulationLibrary LoadDirectory(string directory)
    {
        var lib = new RegulationLibrary();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order())
            foreach (var r in JsonSerializer.Deserialize<List<RegulationReference>>(File.ReadAllText(file), JsonOptions) ?? [])
                lib.Upsert(r);
        return lib;
    }

    /// <summary>Writes one file per jurisdiction group: us-federal.json, model-industry.json, us-states.json.</summary>
    public void SaveDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        var groups = _byId.Values.GroupBy(r => r.Jurisdiction switch
        {
            Jurisdictions.Federal => "us-federal.json",
            Jurisdictions.ModelOrIndustry => "model-industry.json",
            _ => "us-states.json",
        });
        foreach (var g in groups)
            File.WriteAllText(Path.Combine(directory, g.Key),
                JsonSerializer.Serialize(g.OrderBy(r => r.Jurisdiction).ThenBy(r => r.Id).ToList(), JsonOptions));
    }
}
