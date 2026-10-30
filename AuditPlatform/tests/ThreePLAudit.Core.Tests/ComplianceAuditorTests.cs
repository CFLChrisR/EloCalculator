using ThreePLAudit.Core.Auditing;
using ThreePLAudit.Core.Intake;
using ThreePLAudit.Core.Regulations;
using static ThreePLAudit.Core.Tests.TestSupport;

namespace ThreePLAudit.Core.Tests;

public class ComplianceAuditorTests
{
    private static RegulationReference Req(string id, string jurisdiction, RequirementKind kind, string type, string handles = "*", int? months = null) => new()
    {
        Id = id, Jurisdiction = jurisdiction, Title = id, Citation = id, SourceUrl = "https://example.gov/" + id,
        Requirement = new ComplianceRequirement { Kind = kind, RecordType = type, AppliesToHandles = [handles], MaxAgeMonths = months },
    };

    private static Facility Facility(string state = "FL", params string[] handles) => new()
    {
        FacilityId = "F1", Name = "DC", State = state, Handles = [.. handles],
    };

    [Fact]
    public void Requirements_come_from_the_library_and_respect_jurisdiction_and_handles()
    {
        var lib = new RegulationLibrary(
        [
            Req("US-FOOD", "US", RequirementKind.Credential, "FoodReg", "Food"),
            Req("TX-LIC", "TX", RequirementKind.Credential, "TxLicense"),
            Req("FL-LIC", "FL", RequirementKind.Credential, "FlLicense"),
            Req("MODEL-X", "MODEL", RequirementKind.Credential, "NotLaw"),
        ]);
        var findings = Run(new ComplianceAuditor(), Empty() with { Facilities = [Facility("FL", "Food")] }, lib);
        Assert.Equal(new[] { "FL-LIC", "US-FOOD" }, findings.SelectMany(f => f.RegulationIds).Order());
        Assert.All(findings, f => Assert.Equal("CP-CRED-MISSING", f.RuleId));
    }

    [Fact]
    public void Expired_and_expiring_credentials_are_graded()
    {
        var lib = new RegulationLibrary([Req("US-A", "US", RequirementKind.Credential, "A"), Req("US-B", "US", RequirementKind.Credential, "B"), Req("US-C", "US", RequirementKind.Credential, "C")]);
        var f = Facility() with
        {
            Credentials =
            [
                new Credential { Type = "A", ExpiresOn = Today.AddDays(-1) },
                new Credential { Type = "B", ExpiresOn = Today.AddDays(30) },
                new Credential { Type = "C", ExpiresOn = Today.AddDays(365) },
            ],
        };
        var rules = Run(new ComplianceAuditor(), Empty() with { Facilities = [f] }, lib).Select(x => x.RuleId).Order();
        Assert.Equal(new[] { "CP-CRED-EXPIRED", "CP-CRED-EXPIRING" }, rules);
    }

    [Fact]
    public void Stale_training_is_flagged_per_employee_using_latest_completion()
    {
        var lib = new RegulationLibrary([Req("US-T", "US", RequirementKind.Training, "Forklift", months: 36)]);
        var f = Facility() with
        {
            Training =
            [
                new TrainingRecord { EmployeeId = "E1", Type = "Forklift", CompletedOn = new(2022, 1, 1) },
                new TrainingRecord { EmployeeId = "E1", Type = "Forklift", CompletedOn = new(2025, 1, 1) },
                new TrainingRecord { EmployeeId = "E2", Type = "Forklift", CompletedOn = new(2023, 1, 1) },
            ],
        };
        var finding = Assert.Single(Run(new ComplianceAuditor(), Empty() with { Facilities = [f] }, lib));
        Assert.Equal("CP-TRAIN-STALE", finding.RuleId);
        Assert.Contains("E2", finding.Title);
    }

    [Fact]
    public void Unverified_sources_lower_confidence_and_say_so()
    {
        var lib = new RegulationLibrary([Req("US-A", "US", RequirementKind.Credential, "A")]);
        var f = Assert.Single(Run(new ComplianceAuditor(), Empty() with { Facilities = [Facility()] }, lib));
        Assert.Equal(0.6, f.Confidence);
        Assert.Contains("Unverified", f.Detail);
    }
}
