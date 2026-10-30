using ThreePLAudit.Core.Auditing;
using static ThreePLAudit.Core.Tests.TestSupport;

namespace ThreePLAudit.Core.Tests;

public class AuditEngineTests
{
    [Fact]
    public void Sample_package_produces_expected_findings_and_dispositions()
    {
        var run = AuditEngine.CreateDefault().Run(Sample(), SeedLibrary(), Now);

        Assert.DoesNotContain(run.ValidationIssues, i => i.IsError);
        var rules = run.Findings.Select(f => f.RuleId).ToHashSet();
        foreach (var expected in new[]
                 {
                     "WH-RATE", "WH-UNCONTRACTED", "WH-MATH", "WH-QTY", "WH-DUP", "WH-APPROVAL", "WH-MINIMUM",
                     "FR-DUP", "FR-FUEL", "FR-ACC", "FR-WEIGHT", "FR-CLASS", "FR-GSR",
                     "DD-SHORTAGE", "DD-SHORTAGE-PARTIAL", "DD-LATE", "DD-ASN", "DD-DUP", "DD-NO-EVIDENCE",
                     "CP-CRED-EXPIRED", "CP-TRAIN-MISSING", "CP-TRAIN-STALE",
                 })
            Assert.Contains(expected, rules);

        Assert.All(run.Findings.Where(f => f.Disposition == Disposition.AutoDispute),
            f => Assert.True(f.Confidence >= 0.9 && f.RecoverableAmount >= 25m));
        Assert.All(run.Findings.Where(f => f.Category == AuditCategory.Compliance),
            f => Assert.Equal(Disposition.NeedsReview, f.Disposition));
        Assert.Equal(run.Findings.Sum(f => f.RecoverableAmount), run.Summary.TotalRecoverable);
        Assert.Equal(run.Findings.Count, run.Findings.Select(f => f.Id).Distinct().Count());
    }

    [Fact]
    public void Finding_ids_are_stable_across_runs()
    {
        var a = AuditEngine.CreateDefault().Run(Sample(), SeedLibrary(), Now).Findings.Select(f => f.Id).Order();
        var b = AuditEngine.CreateDefault().Run(Sample(), SeedLibrary(), Now.AddDays(1)).Findings.Select(f => f.Id).Order();
        Assert.Equal(a, b);
    }

    [Fact]
    public void Passed_deadline_forces_review()
    {
        var run = AuditEngine.CreateDefault().Run(Sample(), SeedLibrary(), Now.AddYears(1));
        Assert.DoesNotContain(run.Findings, f => f.Disposition == Disposition.AutoDispute && f.Deadline < DateOnly.FromDateTime(Now.AddYears(1).UtcDateTime));
        Assert.Contains(run.Findings, f => f.DispositionReason?.Contains("has passed") == true);
    }

    [Fact]
    public void Thresholds_are_configurable()
    {
        var strict = AuditEngine.CreateDefault(new AuditSettings { AutoDisputeMinConfidence = 0.99 }).Run(Sample(), SeedLibrary(), Now);
        Assert.DoesNotContain(strict.Findings, f => f.Disposition == Disposition.AutoDispute);
    }

    [Fact]
    public void Validation_errors_block_the_audit()
    {
        var bad = Sample() with { Facilities = [Sample().Facilities[0] with { State = "ZZ" }] };
        var run = AuditEngine.CreateDefault().Run(bad, SeedLibrary(), Now);
        Assert.Contains(run.ValidationIssues, i => i.IsError);
        Assert.Empty(run.Findings);
    }

    [Fact]
    public void Dispute_draft_withholds_unverified_citations_and_warns()
    {
        var lib = SeedLibrary();
        var run = AuditEngine.CreateDefault().Run(Sample(), lib, Now);
        var dup = run.Findings.First(f => f.RuleId == "FR-DUP");
        var draft = DisputeDrafter.Draft(dup, run.ClientName, lib, TestSupport.Today);
        Assert.Contains("$483.24", draft.Body);
        Assert.DoesNotContain("Authority:", draft.Body);
        Assert.Contains(draft.Warnings, w => w.Contains("13710"));
    }

    [Fact]
    public void Csv_escapes_fields()
    {
        var run = AuditEngine.CreateDefault().Run(Sample(), SeedLibrary(), Now);
        var csv = ReportWriter.ToCsv(run.Findings);
        Assert.Equal(run.Findings.Count + 1, csv.TrimEnd().Split('\n').Length);
        Assert.StartsWith("FindingId,RuleId", csv);
    }
}
