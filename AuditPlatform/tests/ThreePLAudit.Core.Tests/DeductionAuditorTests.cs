using ThreePLAudit.Core.Auditing;
using ThreePLAudit.Core.Intake;
using static ThreePLAudit.Core.Tests.TestSupport;

namespace ThreePLAudit.Core.Tests;

public class DeductionAuditorTests
{
    private static readonly ShipmentEvidence CleanShipment = new()
    {
        PoNumber = "PO1", BolNumber = "BOL1", ShipDate = new(2026, 9, 5), ShipWindowStart = new(2026, 9, 1), ShipWindowEnd = new(2026, 9, 6),
        ShippedAt = new(2026, 9, 5, 14, 0, 0, TimeSpan.Zero), AsnSentAt = new(2026, 9, 5, 15, 0, 0, TimeSpan.Zero),
        UnitsShipped = 100, PodSigned = true, PodUnitsReceived = 100,
    };

    private static Deduction D(string id, DeductionType type, decimal amount, decimal? units = null, string po = "PO1", int day = 1) =>
        new() { DeductionId = id, Retailer = "MegaMart", Type = type, Amount = amount, PoNumber = po, UnitsClaimed = units, DeductionDate = new(2026, 10, day) };

    private static List<Finding> Audit(ShipmentEvidence shipment, params Deduction[] deductions) => Run(new DeductionAuditor(), Empty() with
    {
        RetailerProfiles = [new RetailerProfile { Retailer = "MegaMart", DisputeWindowDays = 90, AsnDeadlineHours = 4 }],
        Shipments = [shipment],
        Deductions = [.. deductions],
    });

    [Fact]
    public void Clean_pod_makes_shortage_fully_disputable_with_retailer_deadline()
    {
        var f = Assert.Single(Audit(CleanShipment, D("D1", DeductionType.Shortage, 500m, 10)));
        Assert.Equal("DD-SHORTAGE", f.RuleId);
        Assert.Equal(500m, f.RecoverableAmount);
        Assert.Equal(new DateOnly(2026, 10, 1).AddDays(90), f.Deadline);
        Assert.Equal("BOL1", f.Evidence["bol"]);
    }

    [Fact]
    public void Pod_exception_limits_recovery_to_the_overstated_units()
    {
        var shipment = CleanShipment with { PodUnitsReceived = 95, PodExceptions = "5 short" };
        var f = Assert.Single(Audit(shipment, D("D1", DeductionType.Shortage, 200m, 20)));
        Assert.Equal("DD-SHORTAGE-PARTIAL", f.RuleId);
        Assert.Equal(150m, f.RecoverableAmount);
        Assert.Empty(Audit(shipment, D("D1", DeductionType.Shortage, 50m, 5)));
    }

    [Fact]
    public void Ship_window_and_asn_timing_disprove_compliance_chargebacks()
    {
        var findings = Audit(CleanShipment, D("D1", DeductionType.LateShipment, 100m), D("D2", DeductionType.EarlyShipment, 100m), D("D3", DeductionType.Asn, 100m));
        Assert.Equal(new[] { "DD-ASN", "DD-EARLY", "DD-LATE" }, findings.Select(f => f.RuleId).Order());

        var late = CleanShipment with { ShipDate = new(2026, 9, 8) };
        Assert.Empty(Audit(late, D("D1", DeductionType.LateShipment, 100m)));
    }

    [Fact]
    public void Duplicates_are_disputed_and_not_double_counted()
    {
        var findings = Audit(CleanShipment, D("D1", DeductionType.LateShipment, 100m), D("D2", DeductionType.LateShipment, 100m, day: 9));
        Assert.Equal(200m, findings.Sum(f => f.RecoverableAmount));
        Assert.Single(findings, f => f.RuleId == "DD-DUP" && f.EntityRef == "D2");
    }

    [Fact]
    public void Missing_evidence_is_flagged_for_follow_up()
    {
        var f = Assert.Single(Audit(CleanShipment, D("D1", DeductionType.Labeling, 100m, po: "PO-X")));
        Assert.Equal("DD-NO-EVIDENCE", f.RuleId);
        Assert.Equal(0m, f.RecoverableAmount);
    }
}
