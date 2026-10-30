using System.Text;
using ThreePLAudit.Core.Regulations;

namespace ThreePLAudit.Core.Auditing;

public sealed record CitedSource(string Id, string Citation, string? Url, VerificationStatus Status);

public sealed record DisputeDraft
{
    public required string FindingId { get; init; }
    public string? To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public decimal Amount { get; init; }
    public DateOnly? RespondBy { get; init; }
    public List<CitedSource> Citations { get; init; } = [];
    /// <summary>Warnings the sender should clear before sending (unverified citations, passed deadlines).</summary>
    public List<string> Warnings { get; init; } = [];
}

/// <summary>Turns a money finding into a dispute letter for the 3PL, carrier or retailer.</summary>
public static class DisputeDrafter
{
    public static DisputeDraft Draft(Finding f, string clientName, RegulationLibrary library, DateOnly today)
    {
        var cited = f.RegulationIds.Select(library.Get).OfType<RegulationReference>()
                     .Select(r => new CitedSource(r.Id, r.Citation, r.SourceUrl, r.Status)).ToList();
        var warnings = new List<string>();
        foreach (var c in cited.Where(c => c.Status is not (VerificationStatus.HumanVerified or VerificationStatus.SourceMatched)))
            warnings.Add($"Citation {c.Citation} is '{c.Status}'. Verify it before sending, or remove it.");
        if (f.Deadline is { } d && d < today)
            warnings.Add($"The dispute deadline ({d:yyyy-MM-dd}) has passed.");
        if (f.RecoverableAmount <= 0)
            warnings.Add("This finding has no computed recoverable amount; state the requested correction manually.");

        var (noun, ask) = f.Category switch
        {
            AuditCategory.Freight => ("freight bill", "issue a corrected invoice or credit"),
            AuditCategory.Deductions => ("deduction", "reverse the deduction and remit payment"),
            AuditCategory.WarehouseBilling => ("invoice", "issue a credit memo"),
            _ => ("record", "review and respond"),
        };

        var sb = new StringBuilder();
        sb.AppendLine($"To: {f.Counterparty ?? "[counterparty]"}");
        sb.AppendLine();
        sb.AppendLine($"On behalf of {clientName}, we dispute the {noun} referenced below.");
        sb.AppendLine();
        sb.AppendLine($"Reference: {f.EntityRef}");
        if (f.BilledAmount is { } billed) sb.AppendLine($"Amount billed/deducted: {billed:C}");
        if (f.ExpectedAmount is { } exp) sb.AppendLine($"Amount supported: {exp:C}");
        sb.AppendLine($"Amount in dispute: {f.RecoverableAmount:C}");
        sb.AppendLine();
        sb.AppendLine("Basis:");
        sb.AppendLine(f.Detail);
        if (f.Evidence.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Supporting data:");
            foreach (var (k, v) in f.Evidence) sb.AppendLine($"  {k}: {v}");
        }
        var citable = cited.Where(c => c.Status is VerificationStatus.HumanVerified or VerificationStatus.SourceMatched).ToList();
        if (citable.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Authority:");
            foreach (var c in citable) sb.AppendLine($"  {c.Citation}{(c.Url is null ? "" : $" ({c.Url})")}");
        }
        sb.AppendLine();
        sb.AppendLine($"Please {ask} for {f.RecoverableAmount:C}" + (f.Deadline is { } dl && dl >= today ? $". This dispute is submitted within the period ending {dl:yyyy-MM-dd}." : "."));
        sb.AppendLine();
        sb.AppendLine($"Audit finding {f.Id} ({f.RuleId}).");

        return new DisputeDraft
        {
            FindingId = f.Id,
            To = f.Counterparty,
            Subject = $"Dispute: {f.EntityRef} ({f.RecoverableAmount:C})",
            Body = sb.ToString(),
            Amount = f.RecoverableAmount,
            RespondBy = f.Deadline,
            Citations = cited,
            Warnings = warnings,
        };
    }
}
