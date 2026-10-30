using ThreePLAudit.Core.Intake;

namespace ThreePLAudit.Core.Auditing;

/// <summary>LTL and parcel freight bills checked against carrier contracts, fuel tables and shipment facts.</summary>
public sealed class FreightAuditor : IAuditModule
{
    public const string BillingDisputeStatute = "US-49USC13710";
    public const string OverchargeLimitations = "US-49USC14705";
    public const string DensityStandard = "MODEL-NMFC-DENSITY";

    public AuditCategory Category => AuditCategory.Freight;

    public IReadOnlyDictionary<string, string> Rules { get; } = new Dictionary<string, string>
    {
        ["FR-DUP"] = "Same PRO/tracking number billed more than once.",
        ["FR-RATE"] = "Base charge above the contracted lane rate.",
        ["FR-FUEL"] = "Fuel surcharge above the contract fuel table for the ship-week diesel price.",
        ["FR-ACC"] = "Accessorial not in the contract, not requested, or above its cap.",
        ["FR-WEIGHT"] = "Billed weight above actual shipped weight.",
        ["FR-DIM"] = "Parcel billed weight above the greater of actual and dimensional weight.",
        ["FR-CLASS"] = "Billed freight class higher than the density-based class.",
        ["FR-GSR"] = "Delivered after the guaranteed date: refund eligible.",
        ["FR-TOTAL"] = "Bill total does not equal base + fuel + accessorials.",
    };

    public IEnumerable<Finding> Evaluate(AuditContext ctx)
    {
        var p = ctx.Package;
        var s = ctx.Settings;
        var findings = new List<Finding>();
        // Later copies of a duplicated bill are disputed whole, so they skip the line-level checks.
        var duplicateCopies = new HashSet<FreightBill>(ReferenceEqualityComparer.Instance);

        foreach (var dup in p.FreightBills.GroupBy(b => (Carrier: b.Carrier.ToUpperInvariant(), Pro: b.ProNumber.Trim())).Where(g => g.Count() > 1))
        {
            var bills = dup.OrderBy(b => b.ReceivedDate).ToList();
            var sameAmounts = bills.Select(b => b.Total).Distinct().Count() == 1;
            var later = bills.Skip(1).ToList();
            if (sameAmounts) duplicateCopies.UnionWith(later);
            findings.Add(Build(ctx, later[0], "FR-DUP", Severity.High,
                $"PRO {dup.Key.Pro} billed {bills.Count} times",
                $"{bills[0].Carrier} PRO {dup.Key.Pro} appears on bills {string.Join(", ", bills.Select(b => b.BillNumber))}." +
                (sameAmounts ? " The amounts are identical." : " The amounts differ; one may be a legitimate balance-due or corrected bill."),
                later.Sum(b => b.Total), sameAmounts ? 0.95 : 0.7, later.Sum(b => b.Total), 0,
                entitySuffix: string.Join(",", bills.Select(b => b.BillNumber))));
        }

        foreach (var bill in p.FreightBills.Where(b => !duplicateCopies.Contains(b)))
        {
            var contract = FindContract(p, bill);
            // A late guaranteed delivery refunds the whole bill, so the line-level checks would double count.
            if (bill.GuaranteedDeliveryDate is { } guaranteed && bill.DeliveryDate is { } delivered && delivered > guaranteed
                && contract?.GuaranteedServiceRefunds != false)
            {
                findings.Add(Build(ctx, bill, "FR-GSR", Severity.Medium, $"Late delivery on guaranteed service: {bill.BillNumber}",
                    $"Guaranteed {guaranteed:yyyy-MM-dd}, delivered {delivered:yyyy-MM-dd}. Transportation charges are refundable under the carrier's service guarantee, " +
                    "unless an exception applies (weather, incorrect address, and the like).",
                    bill.Total, contract is null ? 0.75 : 0.9, bill.Total, 0));
                continue;
            }

            var accessorialTotal = bill.Accessorials.Sum(a => a.Amount);

            var expectedTotal = bill.BaseCharge + bill.FuelSurcharge + accessorialTotal;
            if (bill.Total - expectedTotal > s.FreightChargeToleranceAmount)
                findings.Add(Build(ctx, bill, "FR-TOTAL", Severity.High, $"Bill {bill.BillNumber} total exceeds its components",
                    $"Base {bill.BaseCharge:C} + fuel {bill.FuelSurcharge:C} + accessorials {accessorialTotal:C} = {expectedTotal:C}, but the bill totals {bill.Total:C}.",
                    bill.Total - expectedTotal, 0.97, bill.Total, expectedTotal));

            decimal? expectedBase = null;
            if (contract is not null && FindLane(contract, bill) is { } lane)
            {
                expectedBase = ExpectedBase(lane, bill.BilledWeightLbs);
                if (bill.BaseCharge - expectedBase.Value > s.FreightChargeToleranceAmount)
                    findings.Add(Build(ctx, bill, "FR-RATE", Severity.High, $"Base charge above contract on {bill.BillNumber}",
                        $"Lane {lane.Origin}→{lane.Destination}: {DescribeLane(lane)} on {bill.BilledWeightLbs:N0} lb gives {expectedBase:C}; billed {bill.BaseCharge:C}.",
                        bill.BaseCharge - expectedBase.Value, 0.9, bill.BaseCharge, expectedBase));

                if (bill.ActualWeightLbs is { } actual && bill.Mode != FreightMode.Parcel
                    && bill.BilledWeightLbs > actual * (1 + s.WeightTolerancePercent / 100m))
                {
                    var atActual = ExpectedBase(lane, actual);
                    findings.Add(Build(ctx, bill, "FR-WEIGHT", Severity.Medium, $"Billed weight above actual on {bill.BillNumber}",
                        $"Billed {bill.BilledWeightLbs:N0} lb; shipper records {actual:N0} lb. At actual weight the contract base is {atActual:C}. " +
                        "Check for a carrier reweigh certificate before disputing.",
                        Math.Min(bill.BaseCharge, expectedBase.Value) - atActual, 0.8, bill.BaseCharge, atActual));
                }
            }

            if (contract is { FuelTable.Count: > 0 } && bill.DieselPrice is { } diesel)
            {
                var band = contract.FuelTable.FirstOrDefault(b => diesel >= b.PriceFrom && diesel < b.PriceTo);
                if (band is null)
                    findings.Add(Build(ctx, bill, "FR-FUEL", Severity.Low, $"Diesel price outside fuel table on {bill.BillNumber}",
                        $"Diesel {diesel:C}/gal is not covered by any band in the {contract.Carrier} fuel table. Confirm the fuel schedule.",
                        0, 0.5, bill.FuelSurcharge, null));
                else
                {
                    var fuelBase = Math.Min(bill.BaseCharge, expectedBase ?? bill.BaseCharge);
                    var expectedFuel = Math.Round(fuelBase * band.SurchargePercent / 100m, 2);
                    if (bill.FuelSurcharge - expectedFuel > s.FreightChargeToleranceAmount)
                        findings.Add(Build(ctx, bill, "FR-FUEL", Severity.Medium, $"Fuel surcharge above table on {bill.BillNumber}",
                            $"Diesel {diesel:C}/gal falls in the {band.PriceFrom:C}–{band.PriceTo:C} band at {band.SurchargePercent}% of {fuelBase:C} = {expectedFuel:C}; billed {bill.FuelSurcharge:C}.",
                            bill.FuelSurcharge - expectedFuel, 0.9, bill.FuelSurcharge, expectedFuel));
                }
            }

            if (contract is not null)
                foreach (var acc in bill.Accessorials)
                {
                    var allowed = contract.Accessorials.FirstOrDefault(a => a.Code.Equals(acc.Code, StringComparison.OrdinalIgnoreCase));
                    var requested = bill.RequestedAccessorials.Contains(acc.Code, StringComparer.OrdinalIgnoreCase);
                    var suffix = acc.Code;
                    if (allowed is null)
                        findings.Add(Build(ctx, bill, "FR-ACC", Severity.Medium, $"Uncontracted accessorial '{acc.Code}' on {bill.BillNumber}",
                            $"'{acc.Code}' ({acc.Amount:C}) is not an accessorial in the {contract.Carrier} contract.",
                            acc.Amount, requested ? 0.6 : 0.85, acc.Amount, 0, suffix));
                    else if (allowed.RequiresRequest && !requested)
                        findings.Add(Build(ctx, bill, "FR-ACC", Severity.Medium, $"Unrequested accessorial '{acc.Code}' on {bill.BillNumber}",
                            $"'{acc.Code}' ({acc.Amount:C}) was billed but not requested on the BOL. Ask the carrier for proof the service was performed.",
                            acc.Amount, 0.8, acc.Amount, 0, suffix));
                    else if (allowed.MaxAmount is { } cap && acc.Amount - cap > s.FreightChargeToleranceAmount)
                        findings.Add(Build(ctx, bill, "FR-ACC", Severity.Medium, $"Accessorial '{acc.Code}' above cap on {bill.BillNumber}",
                            $"'{acc.Code}' billed {acc.Amount:C}; contract caps it at {cap:C}.",
                            acc.Amount - cap, 0.95, acc.Amount, cap, suffix));
                }

            if (bill.Mode == FreightMode.Parcel && bill.Dimensions is { } dims && contract?.DimDivisor is { } divisor && divisor > 0)
            {
                var dimWeight = Math.Ceiling(dims.CubicInches / divisor);
                var actualWeight = Math.Ceiling(bill.ActualWeightLbs ?? 0);
                var billable = Math.Max(dimWeight, actualWeight);
                if (billable > 0 && bill.BilledWeightLbs > billable)
                    findings.Add(Build(ctx, bill, "FR-DIM", Severity.Medium, $"Billed weight above billable weight on {bill.BillNumber}",
                        $"Dimensional weight {dimWeight} lb ({dims.LengthIn}×{dims.WidthIn}×{dims.HeightIn} in ÷ {divisor}) and actual {actualWeight} lb give billable {billable} lb; billed {bill.BilledWeightLbs} lb. " +
                        "Recoverable is estimated pro rata; confirm against the carrier's rate chart.",
                        bill.BaseCharge * (bill.BilledWeightLbs - billable) / bill.BilledWeightLbs, 0.8, bill.BilledWeightLbs, billable));
            }

            if (bill.Mode == FreightMode.Ltl && bill.Dimensions is { CubicFeet: > 0 } d && bill.ActualWeightLbs is { } w
                && bill.BilledClass is { } billedClass && decimal.TryParse(billedClass, out var billedNum))
            {
                var density = w / d.CubicFeet;
                var densityClass = DensityClass(density, s);
                if (decimal.TryParse(densityClass, out var densityNum) && billedNum > densityNum)
                    findings.Add(Build(ctx, bill, "FR-CLASS", Severity.Medium, $"Class {billedClass} billed; density supports {densityClass} on {bill.BillNumber}",
                        $"{w:N0} lb over {d.CubicFeet:N2} ft³ = {density:N2} lb/ft³, which is class {densityClass} on the density scale. " +
                        "If the commodity has a fixed NMFC item class, that class applies instead. Re-rate before disputing.",
                        0, 0.6, null, null, regulations: [DensityStandard]));
            }

        }

        return findings;
    }

    private static Finding Build(AuditContext ctx, FreightBill bill, string rule, Severity sev, string title, string detail,
        decimal recoverable, double confidence, decimal? billed, decimal? expected, string? entitySuffix = null, List<string>? regulations = null)
    {
        var (deadline, basis, regs) = Deadline(ctx.Settings, bill);
        var entity = entitySuffix is null ? bill.BillNumber : $"{bill.BillNumber}:{entitySuffix}";
        return new Finding
        {
            Id = Finding.MakeId(rule, $"{ctx.Package.PackageId}|{entity}"),
            RuleId = rule, Category = AuditCategory.Freight, Severity = sev, Title = title, Detail = detail,
            EntityType = "FreightBill", EntityRef = bill.BillNumber, Counterparty = bill.Carrier,
            BilledAmount = billed, ExpectedAmount = expected,
            RecoverableAmount = Math.Max(0, Math.Round(recoverable, 2)), Confidence = confidence,
            Deadline = deadline, DeadlineBasis = basis,
            RegulationIds = [.. regs, .. regulations ?? []],
            Evidence = new() { ["pro"] = bill.ProNumber, ["shipDate"] = bill.ShipDate.ToString("yyyy-MM-dd"), ["received"] = bill.ReceivedDate.ToString("yyyy-MM-dd") },
        };
    }

    private static (DateOnly, string, List<string>) Deadline(AuditSettings s, FreightBill bill) => bill.Mode == FreightMode.Parcel
        ? (bill.ReceivedDate.AddDays(s.ParcelClaimWindowDays),
           $"{s.ParcelClaimWindowDays} days from invoice per the parcel carrier's service guide (configurable; confirm against your carrier's terms).", [])
        : (bill.ReceivedDate.AddDays(s.MotorCarrierDisputeDays),
           $"{s.MotorCarrierDisputeDays} days from receipt of the bill to contest it (49 U.S.C. § 13710(a)(3)(B)).",
           [BillingDisputeStatute, OverchargeLimitations]);

    public static string DensityClass(decimal density, AuditSettings s) =>
        s.DensityScale.OrderByDescending(b => b.MinDensity).First(b => density >= b.MinDensity).FreightClass;

    public static decimal ExpectedBase(LaneRate lane, decimal weightLbs)
    {
        var gross = lane.FlatRate ?? (lane.RatePerCwt ?? 0) * weightLbs / 100m;
        var net = gross * (1 - lane.DiscountPercent / 100m);
        return Math.Round(Math.Max(net, lane.MinimumCharge), 2);
    }

    private static string DescribeLane(LaneRate l) =>
        (l.FlatRate is { } f ? $"flat {f:C}" : $"{l.RatePerCwt:C}/cwt") +
        (l.DiscountPercent > 0 ? $" less {l.DiscountPercent}%" : "") +
        (l.MinimumCharge > 0 ? $", minimum {l.MinimumCharge:C}" : "");

    private static CarrierContract? FindContract(AuditPackage p, FreightBill bill) =>
        p.CarrierContracts.FirstOrDefault(c =>
            c.Carrier.Equals(bill.Carrier, StringComparison.OrdinalIgnoreCase) && c.Mode == bill.Mode
            && bill.ShipDate >= c.EffectiveFrom && (c.EffectiveTo is null || bill.ShipDate <= c.EffectiveTo));

    /// <summary>Most specific lane wins: exact match, then ZIP3 or state, then wildcard.</summary>
    public static LaneRate? FindLane(CarrierContract contract, FreightBill bill) =>
        contract.Lanes
            .Select(l => (Lane: l, Score: Score(l.Origin, bill.Origin) * Score(l.Destination, bill.Destination)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Lane)
            .FirstOrDefault();

    private static int Score(string pattern, string value)
    {
        if (pattern == "*") return 1;
        if (pattern.Equals(value, StringComparison.OrdinalIgnoreCase)) return 4;
        if (pattern.Length == 3 && pattern.All(char.IsDigit) && value.StartsWith(pattern, StringComparison.Ordinal)) return 3;
        return 0;
    }
}
