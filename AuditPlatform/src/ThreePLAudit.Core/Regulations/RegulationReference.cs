namespace ThreePLAudit.Core.Regulations;

public enum ReferenceKind { Statute, Regulation, ModelCode, IndustryStandard, AgencyGuidance, BestPractice }

public enum VerificationStatus
{
    /// <summary>Never checked against its source. Do not rely on it.</summary>
    Unverified,
    /// <summary>Has no source URL yet.</summary>
    NeedsSource,
    /// <summary>The source URL failed on the last check.</summary>
    Broken,
    /// <summary>The source loaded, but the expected phrase was not found.</summary>
    PhraseMissing,
    /// <summary>The source loaded and contains the expected phrase. A machine check, not a legal review.</summary>
    SourceMatched,
    /// <summary>A person read the source and confirmed the entry.</summary>
    HumanVerified,
    /// <summary>Human-verified, but the source content has changed since. Re-review.</summary>
    ChangedSinceVerified,
}

public static class Topics
{
    public const string FreightClaims = "freight-claims";
    public const string FreightClassification = "freight-classification";
    public const string WarehouseReceipts = "warehouse-receipts-liens";
    public const string WarehouseLicensing = "warehouse-licensing";
    public const string WorkplaceSafety = "workplace-safety";
    public const string FoodSafety = "food-safety";
    public const string Pharma = "pharma";
    public const string Hazmat = "hazmat";
    public const string SalesTax = "sales-use-tax";
    public const string RetailCompliance = "retail-compliance";
    public const string Benchmarks = "benchmarks";

    public static readonly IReadOnlyList<string> All =
    [
        FreightClaims, FreightClassification, WarehouseReceipts, WarehouseLicensing, WorkplaceSafety,
        FoodSafety, Pharma, Hazmat, SalesTax, RetailCompliance, Benchmarks,
    ];

    /// <summary>Topics where most states have their own law, so each state needs its own entry.</summary>
    public static readonly IReadOnlyList<string> StateCoverage =
        [WarehouseReceipts, WarehouseLicensing, WorkplaceSafety, SalesTax, Pharma];
}

/// <summary>
/// One citable source: a statute, regulation, model code, standard, or best practice. Each
/// entry points at an official source and carries enough metadata to check it again later.
/// </summary>
public sealed record RegulationReference
{
    /// <summary>Stable id, e.g. "US-49USC13710" or "FL-UCC7".</summary>
    public required string Id { get; init; }
    /// <summary>"US", a state code, or "MODEL" for model codes and industry standards.</summary>
    public required string Jurisdiction { get; init; }
    public ReferenceKind Kind { get; init; }
    public List<string> Topics { get; init; } = [];
    public required string Title { get; init; }
    /// <summary>The formal citation, e.g. "49 U.S.C. § 13710(a)(3)".</summary>
    public required string Citation { get; init; }
    public string Summary { get; init; } = "";
    public List<string> KeyPoints { get; init; } = [];
    public string? SourceUrl { get; init; }
    /// <summary>Who publishes the source, e.g. "eCFR (Office of the Federal Register)".</summary>
    public string? SourceAuthority { get; init; }
    public List<string> AlternateUrls { get; init; } = [];
    /// <summary>Text the source must contain for an automated check to count as a match.</summary>
    public string? VerificationPhrase { get; init; }
    /// <summary>If set, the compliance auditor enforces this entry against facility data.</summary>
    public ComplianceRequirement? Requirement { get; init; }
    public string? Notes { get; init; }
    public VerificationRecord Verification { get; init; } = new();

    public VerificationStatus Status => Verification.StatusFor(this);
}

/// <summary>A credential or training that facilities handling certain goods must hold.</summary>
public sealed record ComplianceRequirement
{
    /// <summary>Applies when a facility handles any of these ("*" means every facility).</summary>
    public List<string> AppliesToHandles { get; init; } = ["*"];
    public RequirementKind Kind { get; init; }
    /// <summary>Matches <c>Credential.Type</c> or <c>TrainingRecord.Type</c> in the intake package.</summary>
    public required string RecordType { get; init; }
    /// <summary>For training: how old the latest completion may be. For credentials: unused (expiry dates are checked).</summary>
    public int? MaxAgeMonths { get; init; }
    /// <summary>Only applies to facilities using powered industrial trucks (forklifts).</summary>
    public bool RequiresPoweredIndustrialTrucks { get; init; }
}

public enum RequirementKind { Credential, Training }

public sealed record VerificationRecord
{
    public DateTimeOffset? LastCheckedAt { get; init; }
    public int? LastHttpStatus { get; init; }
    public string? LastError { get; init; }
    public string? LastContentSha256 { get; init; }
    public bool? LastPhraseFound { get; init; }

    public string? VerifiedBy { get; init; }
    public DateTimeOffset? VerifiedAt { get; init; }
    /// <summary>The passage the reviewer relied on, copied from the source.</summary>
    public string? VerifiedExcerpt { get; init; }
    public string? VerifiedContentSha256 { get; init; }

    public VerificationStatus StatusFor(RegulationReference reference)
    {
        if (string.IsNullOrWhiteSpace(reference.SourceUrl)) return VerificationStatus.NeedsSource;
        if (VerifiedBy is not null)
        {
            var changed = LastContentSha256 is not null && VerifiedContentSha256 is not null
                          && LastContentSha256 != VerifiedContentSha256
                          && LastPhraseFound != true;
            return changed ? VerificationStatus.ChangedSinceVerified : VerificationStatus.HumanVerified;
        }
        if (LastCheckedAt is null) return VerificationStatus.Unverified;
        if (LastError is not null || LastHttpStatus is null or >= 400) return VerificationStatus.Broken;
        if (reference.VerificationPhrase is null) return VerificationStatus.SourceMatched;
        return LastPhraseFound == true ? VerificationStatus.SourceMatched : VerificationStatus.PhraseMissing;
    }
}
