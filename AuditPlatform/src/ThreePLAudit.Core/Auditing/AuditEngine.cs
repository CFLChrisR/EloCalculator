using ThreePLAudit.Core.Intake;
using ThreePLAudit.Core.Regulations;

namespace ThreePLAudit.Core.Auditing;

public sealed record AuditContext(AuditPackage Package, AuditSettings Settings, RegulationLibrary Library, DateOnly Today);

/// <summary>A group of related rules (one per audit type). Each finding names the rule that produced it.</summary>
public interface IAuditModule
{
    AuditCategory Category { get; }
    /// <summary>The rule ids this module can produce, with a one-line description each.</summary>
    IReadOnlyDictionary<string, string> Rules { get; }
    IEnumerable<Finding> Evaluate(AuditContext context);
}

public sealed record AuditSummary
{
    public int FindingCount { get; init; }
    public decimal TotalRecoverable { get; init; }
    public decimal AutoDisputeAmount { get; init; }
    public int AutoDisputeCount { get; init; }
    public int NeedsReviewCount { get; init; }
    public Dictionary<AuditCategory, decimal> RecoverableByCategory { get; init; } = [];
    public Dictionary<AuditCategory, int> CountByCategory { get; init; } = [];
    public decimal WarehouseBilledAudited { get; init; }
    public decimal FreightBilledAudited { get; init; }
    public decimal DeductionsAudited { get; init; }
    public DateOnly? EarliestDeadline { get; init; }
}

public sealed record AuditRunResult
{
    public required string RunId { get; init; }
    public required string PackageId { get; init; }
    public required string ClientName { get; init; }
    public DateTimeOffset RunAt { get; init; }
    public List<ValidationIssue> ValidationIssues { get; init; } = [];
    public List<Finding> Findings { get; init; } = [];
    public required AuditSummary Summary { get; init; }
}

public sealed class AuditEngine(IEnumerable<IAuditModule> modules, AuditSettings? settings = null)
{
    public AuditSettings Settings { get; } = settings ?? new AuditSettings();
    public IReadOnlyList<IAuditModule> Modules { get; } = modules.ToList();

    public static AuditEngine CreateDefault(AuditSettings? settings = null) => new(
        [new WarehouseBillingAuditor(), new FreightAuditor(), new DeductionAuditor(), new ComplianceAuditor()],
        settings);

    public AuditRunResult Run(AuditPackage package, RegulationLibrary library, DateTimeOffset? now = null)
    {
        var runAt = now ?? DateTimeOffset.UtcNow;
        var issues = PackageValidator.Validate(package).ToList();
        if (issues.Any(i => i.IsError))
            return new AuditRunResult
            {
                RunId = Guid.NewGuid().ToString("n"), PackageId = package.PackageId, ClientName = package.ClientName,
                RunAt = runAt, ValidationIssues = issues, Summary = new AuditSummary(),
            };

        var context = new AuditContext(package, Settings, library, DateOnly.FromDateTime(runAt.UtcDateTime));
        var findings = Modules
            .SelectMany(m => m.Evaluate(context))
            .GroupBy(f => f.Id).Select(g => g.First())
            .Select(f => Dispose(f, context))
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(f => f.RecoverableAmount)
            .ToList();

        return new AuditRunResult
        {
            RunId = Guid.NewGuid().ToString("n"),
            PackageId = package.PackageId,
            ClientName = package.ClientName,
            RunAt = runAt,
            ValidationIssues = issues,
            Findings = findings,
            Summary = Summarize(package, findings),
        };
    }

    /// <summary>Decides whether a finding can be disputed without a person looking at it.</summary>
    private Finding Dispose(Finding f, AuditContext ctx)
    {
        if (f.Category == AuditCategory.Compliance)
            return f with { Disposition = Disposition.NeedsReview, DispositionReason = "Compliance gaps always need a person." };
        if (f.RecoverableAmount < Settings.MinRecoverableAmount && f.Severity <= Severity.Low)
            return f with { Disposition = Disposition.Informational, DispositionReason = "Below the minimum recoverable amount." };
        if (f.Deadline is { } d && d < ctx.Today)
            return f with { Disposition = Disposition.NeedsReview, DispositionReason = $"Dispute deadline {d:yyyy-MM-dd} has passed." };
        if (f.Confidence >= Settings.AutoDisputeMinConfidence && f.RecoverableAmount >= Settings.AutoDisputeMinAmount)
            return f with { Disposition = Disposition.AutoDispute, DispositionReason = $"Confidence {f.Confidence:P0} and {f.RecoverableAmount:C} recoverable meet the auto-dispute thresholds." };
        return f with
        {
            Disposition = Disposition.NeedsReview,
            DispositionReason = f.Confidence < Settings.AutoDisputeMinConfidence
                ? $"Confidence {f.Confidence:P0} is below the {Settings.AutoDisputeMinConfidence:P0} auto-dispute threshold."
                : $"Recoverable amount is below the {Settings.AutoDisputeMinAmount:C} auto-dispute threshold.",
        };
    }

    private static AuditSummary Summarize(AuditPackage p, List<Finding> findings) => new()
    {
        FindingCount = findings.Count,
        TotalRecoverable = findings.Sum(f => f.RecoverableAmount),
        AutoDisputeAmount = findings.Where(f => f.Disposition == Disposition.AutoDispute).Sum(f => f.RecoverableAmount),
        AutoDisputeCount = findings.Count(f => f.Disposition == Disposition.AutoDispute),
        NeedsReviewCount = findings.Count(f => f.Disposition == Disposition.NeedsReview),
        RecoverableByCategory = findings.GroupBy(f => f.Category).ToDictionary(g => g.Key, g => g.Sum(f => f.RecoverableAmount)),
        CountByCategory = findings.GroupBy(f => f.Category).ToDictionary(g => g.Key, g => g.Count()),
        WarehouseBilledAudited = p.WarehouseInvoices.SelectMany(i => i.Lines).Sum(l => l.Amount),
        FreightBilledAudited = p.FreightBills.Sum(b => b.Total),
        DeductionsAudited = p.Deductions.Sum(d => d.Amount),
        EarliestDeadline = findings.Where(f => f.Disposition == Disposition.AutoDispute && f.Deadline is not null)
                                   .Select(f => f.Deadline).Min(),
    };
}
