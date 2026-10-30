using System.Security.Cryptography;
using System.Text;

namespace ThreePLAudit.Core.Auditing;

public enum AuditCategory { WarehouseBilling, Freight, Deductions, Compliance }

public enum Severity { Info, Low, Medium, High, Critical }

/// <summary>What happens to a finding with no human in the loop.</summary>
public enum Disposition
{
    /// <summary>High confidence and worth recovering: a dispute is drafted automatically.</summary>
    AutoDispute,
    /// <summary>Needs a person: low confidence, deadline passed, or not a money finding.</summary>
    NeedsReview,
    /// <summary>Recorded for the trail; no action needed.</summary>
    Informational,
}

public sealed record Finding
{
    /// <summary>Same rule plus same entity gives the same id, so re-running an audit doesn't duplicate findings.</summary>
    public required string Id { get; init; }
    public required string RuleId { get; init; }
    public AuditCategory Category { get; init; }
    public Severity Severity { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    /// <summary>"InvoiceLine", "FreightBill", "Deduction", "Facility", ...</summary>
    public required string EntityType { get; init; }
    public required string EntityRef { get; init; }
    /// <summary>Who the dispute goes to: 3PL, carrier, or retailer name.</summary>
    public string? Counterparty { get; init; }
    public decimal? BilledAmount { get; init; }
    public decimal? ExpectedAmount { get; init; }
    public decimal RecoverableAmount { get; init; }
    /// <summary>0 to 1: how sure the rule is that this is a real, recoverable error.</summary>
    public double Confidence { get; init; }
    public DateOnly? Deadline { get; init; }
    public string? DeadlineBasis { get; init; }
    public List<string> RegulationIds { get; init; } = [];
    public Dictionary<string, string> Evidence { get; init; } = [];
    public Disposition Disposition { get; init; } = Disposition.NeedsReview;
    public string? DispositionReason { get; init; }

    public static string MakeId(string ruleId, string entityRef)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{ruleId}|{entityRef}"));
        return $"{ruleId}-{Convert.ToHexStringLower(hash)[..12]}";
    }
}
