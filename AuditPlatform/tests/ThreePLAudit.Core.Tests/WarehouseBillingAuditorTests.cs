using ThreePLAudit.Core.Auditing;
using ThreePLAudit.Core.Intake;
using static ThreePLAudit.Core.Tests.TestSupport;

namespace ThreePLAudit.Core.Tests;

public class WarehouseBillingAuditorTests
{
    private static AuditPackage With(List<InvoiceLine> lines, decimal? total = null, decimal? minimum = null, List<ActivitySummary>? activity = null) => Empty() with
    {
        WarehouseContracts =
        [
            new WarehouseContract
            {
                ContractId = "C1", ThreePLName = "3PL", EffectiveFrom = new(2026, 1, 1), MonthlyMinimum = minimum,
                Rates =
                [
                    new ContractRate { ChargeCode = "PICK", Rate = 2.75m },
                    new ContractRate { ChargeCode = "STORAGE", Rate = 18m },
                    new ContractRate { ChargeCode = "LABOR", Rate = 38m, RequiresApproval = true },
                    new ContractRate { ChargeCode = "RETURN", Rate = 1m, LineMinimum = 25m },
                ],
            },
        ],
        WarehouseInvoices =
        [
            new WarehouseInvoice
            {
                InvoiceNumber = "I1", ContractId = "C1", FacilityId = "F1", InvoiceTotal = total,
                ServicePeriodStart = new(2026, 9, 1), ServicePeriodEnd = new(2026, 9, 30), Lines = lines,
            },
        ],
        Activity = activity ?? [],
    };

    private static InvoiceLine Line(string id, string code, decimal qty, decimal rate, decimal? amount = null, string? reference = null, string? approval = null) =>
        new() { LineId = id, ChargeCode = code, Quantity = qty, UnitRate = rate, Amount = amount ?? qty * rate, Reference = reference, ApprovalReference = approval };

    [Fact]
    public void Clean_invoice_has_no_findings()
    {
        var findings = Run(new WarehouseBillingAuditor(), With([Line("1", "PICK", 100, 2.75m), Line("2", "LABOR", 2, 38m, approval: "PO-9")], total: 351m));
        Assert.Empty(findings);
    }

    [Fact]
    public void Rate_above_contract_recovers_difference_times_quantity()
    {
        var f = Assert.Single(Run(new WarehouseBillingAuditor(), With([Line("1", "PICK", 1000, 2.95m)])));
        Assert.Equal("WH-RATE", f.RuleId);
        Assert.Equal(200m, f.RecoverableAmount);
        Assert.Equal("3PL", f.Counterparty);
    }

    [Fact]
    public void Uncontracted_charge_is_fully_recoverable_but_below_auto_threshold()
    {
        var f = Assert.Single(Run(new WarehouseBillingAuditor(), With([Line("1", "RUSH", 1, 250m)])));
        Assert.Equal("WH-UNCONTRACTED", f.RuleId);
        Assert.Equal(250m, f.RecoverableAmount);
        Assert.True(f.Confidence < new AuditSettings().AutoDisputeMinConfidence);
    }

    [Fact]
    public void Line_minimum_is_not_an_extension_error()
    {
        Assert.Empty(Run(new WarehouseBillingAuditor(), With([Line("1", "RETURN", 3, 1m, amount: 25m)])));
        var f = Assert.Single(Run(new WarehouseBillingAuditor(), With([Line("1", "RETURN", 3, 1m, amount: 40m)])));
        Assert.Equal("WH-MATH", f.RuleId);
        Assert.Equal(15m, f.RecoverableAmount);
    }

    [Fact]
    public void Invoice_total_above_line_sum_is_flagged()
    {
        var f = Assert.Single(Run(new WarehouseBillingAuditor(), With([Line("1", "PICK", 10, 2.75m)], total: 40m)));
        Assert.Equal("WH-TOTAL", f.RuleId);
        Assert.Equal(12.50m, f.RecoverableAmount);
    }

    [Fact]
    public void Missing_approval_is_flagged()
    {
        var f = Assert.Single(Run(new WarehouseBillingAuditor(), With([Line("1", "LABOR", 4, 38m)])));
        Assert.Equal("WH-APPROVAL", f.RuleId);
    }

    [Fact]
    public void Quantity_over_activity_beyond_tolerance_is_flagged()
    {
        var activity = new List<ActivitySummary> { new() { FacilityId = "F1", ChargeCode = "STORAGE", PeriodStart = new(2026, 9, 1), PeriodEnd = new(2026, 9, 30), Quantity = 1000 } };
        Assert.Empty(Run(new WarehouseBillingAuditor(), With([Line("1", "STORAGE", 1015, 18m)], activity: activity)));
        var f = Assert.Single(Run(new WarehouseBillingAuditor(), With([Line("1", "STORAGE", 1100, 18m)], activity: activity)));
        Assert.Equal("WH-QTY", f.RuleId);
        Assert.Equal(1800m, f.RecoverableAmount);
    }

    [Fact]
    public void Same_reference_billed_twice_recovers_the_copy()
    {
        var f = Assert.Single(Run(new WarehouseBillingAuditor(), With([Line("1", "PICK", 1, 2.75m, reference: "ORD-1"), Line("2", "PICK", 1, 2.75m, reference: "ORD-1")])));
        Assert.Equal("WH-DUP", f.RuleId);
        Assert.Equal(2.75m, f.RecoverableAmount);
    }

    [Fact]
    public void Minimum_top_up_is_limited_to_the_gap()
    {
        var lines = new List<InvoiceLine> { Line("1", "PICK", 1000, 2.75m), Line("2", "MINIMUM", 1, 3000m) };
        var f = Assert.Single(Run(new WarehouseBillingAuditor(), With(lines, minimum: 5000m)));
        Assert.Equal("WH-MINIMUM", f.RuleId);
        Assert.Equal(3000m - (5000m - 2750m), f.RecoverableAmount);
    }
}
