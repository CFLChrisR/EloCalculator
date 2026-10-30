using ThreePLAudit.Core.Intake;
using ThreePLAudit.Core.Regulations;

namespace ThreePLAudit.Core.Auditing;

/// <summary>
/// Checks facilities against every library entry that carries a <see cref="ComplianceRequirement"/>.
/// The rules live in the library, not in code: adding a state requirement there makes it enforced.
/// Federal entries apply to every facility; state entries apply to facilities in that state.
/// </summary>
public sealed class ComplianceAuditor : IAuditModule
{
    public AuditCategory Category => AuditCategory.Compliance;

    public IReadOnlyDictionary<string, string> Rules { get; } = new Dictionary<string, string>
    {
        ["CP-CRED-MISSING"] = "Required license, registration or permit not on file.",
        ["CP-CRED-EXPIRED"] = "Required credential has expired.",
        ["CP-CRED-EXPIRING"] = "Required credential expires soon.",
        ["CP-TRAIN-MISSING"] = "No record of required training.",
        ["CP-TRAIN-STALE"] = "Employee's latest required training is older than allowed.",
    };

    public IEnumerable<Finding> Evaluate(AuditContext ctx)
    {
        var requirements = ctx.Library.WithRequirements()
            .Where(r => r.Jurisdiction != Jurisdictions.ModelOrIndustry)
            .ToList();

        foreach (var facility in ctx.Package.Facilities)
        foreach (var reference in requirements)
        {
            var req = reference.Requirement!;
            if (!Applies(reference, req, facility)) continue;

            var findings = req.Kind == RequirementKind.Credential
                ? CheckCredential(ctx, facility, reference, req)
                : CheckTraining(ctx, facility, reference, req);
            foreach (var f in findings) yield return f;
        }
    }

    private static bool Applies(RegulationReference reference, ComplianceRequirement req, Facility f)
    {
        if (reference.Jurisdiction != Jurisdictions.Federal && !reference.Jurisdiction.Equals(f.State, StringComparison.OrdinalIgnoreCase))
            return false;
        if (req.RequiresPoweredIndustrialTrucks && !f.UsesPoweredIndustrialTrucks) return false;
        return req.AppliesToHandles.Contains("*") || req.AppliesToHandles.Any(h => f.Handles.Contains(h, StringComparer.OrdinalIgnoreCase));
    }

    private static IEnumerable<Finding> CheckCredential(AuditContext ctx, Facility f, RegulationReference r, ComplianceRequirement req)
    {
        var held = f.Credentials.Where(c => c.Type.Equals(req.RecordType, StringComparison.OrdinalIgnoreCase))
                                .OrderByDescending(c => c.ExpiresOn ?? DateOnly.MaxValue).FirstOrDefault();
        if (held is null)
        {
            yield return Build(ctx, f, r, "CP-CRED-MISSING", Severity.Critical, $"{f.Name}: no {req.RecordType} on file",
                $"{r.Title} ({r.Citation}) applies to this facility, and no '{req.RecordType}' credential is in the package.", req.RecordType);
            yield break;
        }
        if (held.ExpiresOn is not { } exp) yield break;

        if (exp < ctx.Today)
            yield return Build(ctx, f, r, "CP-CRED-EXPIRED", Severity.High, $"{f.Name}: {req.RecordType} expired {exp:yyyy-MM-dd}",
                $"{req.RecordType} {held.Number} expired on {exp:yyyy-MM-dd}. Required by {r.Citation}.", req.RecordType);
        else if (exp <= ctx.Today.AddDays(ctx.Settings.CredentialExpiryWarningDays))
            yield return Build(ctx, f, r, "CP-CRED-EXPIRING", Severity.Medium, $"{f.Name}: {req.RecordType} expires {exp:yyyy-MM-dd}",
                $"{req.RecordType} {held.Number} expires within {ctx.Settings.CredentialExpiryWarningDays} days. Required by {r.Citation}.", req.RecordType);
    }

    private static IEnumerable<Finding> CheckTraining(AuditContext ctx, Facility f, RegulationReference r, ComplianceRequirement req)
    {
        var records = f.Training.Where(t => t.Type.Equals(req.RecordType, StringComparison.OrdinalIgnoreCase)).ToList();
        if (records.Count == 0)
        {
            yield return Build(ctx, f, r, "CP-TRAIN-MISSING", Severity.High, $"{f.Name}: no {req.RecordType} records",
                $"{r.Title} ({r.Citation}) applies to this facility, and no '{req.RecordType}' training records are in the package.", req.RecordType);
            yield break;
        }
        if (req.MaxAgeMonths is not { } months) yield break;

        var cutoff = ctx.Today.AddMonths(-months);
        foreach (var latest in records.GroupBy(t => t.EmployeeId).Select(g => g.MaxBy(t => t.CompletedOn)!))
            if (latest.CompletedOn < cutoff)
                yield return Build(ctx, f, r, "CP-TRAIN-STALE", Severity.Medium,
                    $"{f.Name}: {req.RecordType} overdue for {latest.EmployeeId}",
                    $"Last completed {latest.CompletedOn:yyyy-MM-dd}; {r.Citation} requires it at least every {months} months (due {latest.CompletedOn.AddMonths(months):yyyy-MM-dd}).",
                    $"{req.RecordType}:{latest.EmployeeId}");
    }

    private static Finding Build(AuditContext ctx, Facility f, RegulationReference r, string rule, Severity sev, string title, string detail, string suffix)
    {
        var status = r.Status;
        var verified = status is VerificationStatus.HumanVerified;
        return new Finding
        {
            Id = Finding.MakeId(rule, $"{ctx.Package.PackageId}|{f.FacilityId}|{r.Id}|{suffix}"),
            RuleId = rule, Category = AuditCategory.Compliance, Severity = sev, Title = title,
            Detail = verified ? detail : $"{detail} Note: the library entry {r.Id} is '{status}', so confirm the requirement against the source before acting.",
            EntityType = "Facility", EntityRef = f.FacilityId, Counterparty = f.Operator,
            RecoverableAmount = 0, Confidence = verified ? 0.9 : 0.6,
            RegulationIds = [r.Id],
            Evidence = new() { ["state"] = f.State, ["referenceStatus"] = status.ToString(), ["source"] = r.SourceUrl ?? "(none)" },
        };
    }
}
