using ThreePLAudit.Core.Auditing;
using ThreePLAudit.Core.Intake;
using static ThreePLAudit.Core.Tests.TestSupport;

namespace ThreePLAudit.Core.Tests;

public class FreightAuditorTests
{
    private static readonly CarrierContract Ltl = new()
    {
        Carrier = "RF", Mode = FreightMode.Ltl, EffectiveFrom = new(2026, 1, 1),
        Lanes =
        [
            new LaneRate { Origin = "328", Destination = "752", RatePerCwt = 40m, DiscountPercent = 70m, MinimumCharge = 100m },
            new LaneRate { Origin = "*", Destination = "*", RatePerCwt = 60m, DiscountPercent = 0m, MinimumCharge = 150m },
        ],
        FuelTable = [new FuelBand { PriceFrom = 3.5m, PriceTo = 4m, SurchargePercent = 30m }],
        Accessorials = [new AllowedAccessorial { Code = "LIFTGATE", MaxAmount = 75m }],
    };

    private static FreightBill Bill(string number = "B1", string pro = "P1", decimal baseCharge = 240m, decimal fuel = 72m,
                                    List<BilledAccessorial>? acc = null, decimal? total = null) => new()
    {
        BillNumber = number, Carrier = "RF", Mode = FreightMode.Ltl, ProNumber = pro,
        ShipDate = new(2026, 9, 3), ReceivedDate = new(2026, 9, 20), Origin = "32801", Destination = "75201",
        BilledWeightLbs = 2000, BaseCharge = baseCharge, FuelSurcharge = fuel, DieselPrice = 3.8m,
        Accessorials = acc ?? [], Total = total ?? baseCharge + fuel + (acc?.Sum(a => a.Amount) ?? 0),
    };

    private static List<Finding> Audit(params FreightBill[] bills) =>
        Run(new FreightAuditor(), Empty() with { CarrierContracts = [Ltl], FreightBills = [.. bills] });

    [Fact]
    public void Correct_bill_has_no_findings() => Assert.Empty(Audit(Bill()));

    [Fact]
    public void Most_specific_lane_wins()
    {
        var lane = FreightAuditor.FindLane(Ltl, Bill());
        Assert.Equal("328", lane!.Origin);
        Assert.Equal(240m, FreightAuditor.ExpectedBase(lane, 2000));
        Assert.Equal(100m, FreightAuditor.ExpectedBase(lane, 100)); // minimum charge applies
    }

    [Fact]
    public void Base_overcharge_gets_the_federal_180_day_deadline()
    {
        var f = Assert.Single(Audit(Bill(baseCharge: 300m, fuel: 72m)));
        Assert.Equal("FR-RATE", f.RuleId);
        Assert.Equal(60m, f.RecoverableAmount);
        Assert.Equal(new DateOnly(2026, 9, 20).AddDays(180), f.Deadline);
        Assert.Contains(FreightAuditor.BillingDisputeStatute, f.RegulationIds);
    }

    [Fact]
    public void Fuel_is_measured_against_the_contract_base_not_an_inflated_base()
    {
        var findings = Audit(Bill(baseCharge: 300m, fuel: 90m));
        var fuel = Assert.Single(findings, f => f.RuleId == "FR-FUEL");
        Assert.Equal(90m - 72m, fuel.RecoverableAmount);
    }

    [Fact]
    public void Unrequested_accessorial_and_cap_breach_are_separate_rules()
    {
        var unrequested = Assert.Single(Audit(Bill(acc: [new("LIFTGATE", 75m)])));
        Assert.Equal("FR-ACC", unrequested.RuleId);
        Assert.Equal(75m, unrequested.RecoverableAmount);

        var requested = Bill(acc: [new("LIFTGATE", 95m)]) with { RequestedAccessorials = ["LIFTGATE"] };
        var cap = Assert.Single(Audit(requested));
        Assert.Equal(20m, cap.RecoverableAmount);

        var unknown = Assert.Single(Audit(Bill(acc: [new("DETENTION", 50m)])));
        Assert.Contains("Uncontracted", unknown.Title);
    }

    [Fact]
    public void Identical_duplicate_is_disputed_once_and_not_re_audited()
    {
        var findings = Audit(Bill("B1", baseCharge: 300m), Bill("B2", baseCharge: 300m) with { ReceivedDate = new(2026, 9, 25) });
        Assert.Single(findings, f => f.RuleId == "FR-DUP" && f.RecoverableAmount == 372m);
        Assert.Single(findings, f => f.RuleId == "FR-RATE"); // only the original bill
    }

    [Theory]
    [InlineData(55, "50")]
    [InlineData(18, "70")]
    [InlineData(9, "100")]
    [InlineData(0.5, "400")]
    public void Density_scale_maps_to_class(double density, string expected) =>
        Assert.Equal(expected, FreightAuditor.DensityClass((decimal)density, new AuditSettings()));

    [Fact]
    public void Class_above_density_class_needs_review()
    {
        var bill = Bill() with { ActualWeightLbs = 2000, BilledClass = "100", Dimensions = new(48, 40, 60, 2) };
        var f = Assert.Single(Audit(bill));
        Assert.Equal("FR-CLASS", f.RuleId);
        Assert.True(f.Confidence < 0.9);
    }

    [Fact]
    public void Late_guaranteed_parcel_refunds_whole_bill_and_skips_other_checks()
    {
        var parcel = new CarrierContract { Carrier = "PC", Mode = FreightMode.Parcel, EffectiveFrom = new(2026, 1, 1), DimDivisor = 139 };
        var bill = new FreightBill
        {
            BillNumber = "PC1", Carrier = "PC", Mode = FreightMode.Parcel, ProNumber = "T1", ShipDate = new(2026, 9, 20),
            GuaranteedDeliveryDate = new(2026, 9, 22), DeliveryDate = new(2026, 9, 24), ReceivedDate = new(2026, 10, 20),
            Origin = "32801", Destination = "10001", BilledWeightLbs = 34, ActualWeightLbs = 9,
            Dimensions = new(20, 16, 12), BaseCharge = 40m, FuelSurcharge = 8m, Total = 48m,
        };
        var f = Assert.Single(Run(new FreightAuditor(), Empty() with { CarrierContracts = [parcel], FreightBills = [bill] }));
        Assert.Equal("FR-GSR", f.RuleId);
        Assert.Equal(48m, f.RecoverableAmount);
        Assert.Equal(new DateOnly(2026, 11, 4), f.Deadline);

        var waived = parcel with { GuaranteedServiceRefunds = false };
        var findings = Run(new FreightAuditor(), Empty() with { CarrierContracts = [waived], FreightBills = [bill] });
        Assert.DoesNotContain(findings, x => x.RuleId == "FR-GSR");
        Assert.Contains(findings, x => x.RuleId == "FR-DIM");
    }
}
