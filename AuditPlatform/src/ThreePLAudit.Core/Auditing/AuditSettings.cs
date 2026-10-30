namespace ThreePLAudit.Core.Auditing;

/// <summary>Tolerances and automation thresholds. Defaults are conservative; tune per client.</summary>
public sealed record AuditSettings
{
    /// <summary>Findings at or above this confidence (and <see cref="AutoDisputeMinAmount"/>) are disputed without review.</summary>
    public double AutoDisputeMinConfidence { get; init; } = 0.9;
    public decimal AutoDisputeMinAmount { get; init; } = 25m;
    /// <summary>Below this a money finding is informational; recovery isn't worth the effort.</summary>
    public decimal MinRecoverableAmount { get; init; } = 1m;

    public decimal RateToleranceAmount { get; init; } = 0.01m;
    /// <summary>Billed quantity may exceed recorded activity by this fraction before it's flagged.</summary>
    public decimal QuantityTolerancePercent { get; init; } = 2m;
    public decimal WeightTolerancePercent { get; init; } = 2m;
    public decimal FreightChargeToleranceAmount { get; init; } = 1m;

    /// <summary>Charge codes treated as monthly-minimum top-ups.</summary>
    public HashSet<string> MinimumChargeCodes { get; init; } = new(StringComparer.OrdinalIgnoreCase) { "MIN", "MINIMUM", "MONTHLY_MIN" };

    /// <summary>
    /// Days a shipper has to contest a motor carrier bill. Basis: 49 U.S.C. § 13710(a)(3)(B), 180 days
    /// from receipt of the bill. Applies to carriers subject to that statute; contracts may differ.
    /// </summary>
    public int MotorCarrierDisputeDays { get; init; } = 180;
    /// <summary>
    /// Parcel carriers set claim windows in their own service guides, and these are often much
    /// shorter (commonly about 15 days for late-delivery refunds). Confirm against your carrier's terms.
    /// </summary>
    public int ParcelClaimWindowDays { get; init; } = 15;

    /// <summary>Credentials expiring within this many days are flagged as a warning.</summary>
    public int CredentialExpiryWarningDays { get; init; } = 60;

    /// <summary>
    /// LTL density table: (minimum lb/ft³, class). Default is the 13-tier density scale NMFTA
    /// adopted in 2025 (Docket 2025-1). Confirm against the current NMFC before relying on it,
    /// and note that many commodities have a fixed NMFC class that overrides density.
    /// </summary>
    public List<DensityBreak> DensityScale { get; init; } =
    [
        new(50m, "50"), new(35m, "55"), new(30m, "60"), new(22.5m, "65"), new(15m, "70"),
        new(12m, "85"), new(10m, "92.5"), new(8m, "100"), new(6m, "125"), new(4m, "175"),
        new(2m, "250"), new(1m, "300"), new(0m, "400"),
    ];
}

public sealed record DensityBreak(decimal MinDensity, string FreightClass);
