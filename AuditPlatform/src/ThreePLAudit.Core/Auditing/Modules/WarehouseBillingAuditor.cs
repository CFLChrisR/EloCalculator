using ThreePLAudit.Core.Intake;

namespace ThreePLAudit.Core.Auditing;

/// <summary>3PL invoices tested against the contracted rate card and the client's own activity data.</summary>
public sealed class WarehouseBillingAuditor : IAuditModule
{
    public AuditCategory Category => AuditCategory.WarehouseBilling;

    public IReadOnlyDictionary<string, string> Rules { get; } = new Dictionary<string, string>
    {
        ["WH-RATE"] = "Unit rate billed above the contracted rate.",
        ["WH-UNCONTRACTED"] = "Charge code not in the contract rate card.",
        ["WH-MATH"] = "Line amount does not equal quantity × rate (after line minimums).",
        ["WH-TOTAL"] = "Invoice total does not equal the sum of its lines.",
        ["WH-QTY"] = "Billed quantity exceeds the client's recorded activity.",
        ["WH-DUP"] = "Same charge billed more than once.",
        ["WH-APPROVAL"] = "Charge requires client approval but has no approval reference.",
        ["WH-MINIMUM"] = "Monthly minimum top-up billed when activity already met (or exceeded the gap to) the minimum.",
        ["WH-PERIOD"] = "Service period falls outside the contract's effective dates.",
    };

    public IEnumerable<Finding> Evaluate(AuditContext ctx)
    {
        var p = ctx.Package;
        var s = ctx.Settings;
        var findings = new List<Finding>();

        foreach (var inv in p.WarehouseInvoices)
        {
            var contract = FindContract(p, inv);
            var counterparty = contract?.ThreePLName;

            Finding Make(string rule, Severity sev, string entityRef, string title, string detail, decimal recoverable,
                         double confidence, decimal? billed = null, decimal? expected = null, Dictionary<string, string>? evidence = null) => new()
            {
                Id = Finding.MakeId(rule, $"{p.PackageId}|{entityRef}"),
                RuleId = rule, Category = Category, Severity = sev, Title = title, Detail = detail,
                EntityType = entityRef.Contains('#') ? "InvoiceLine" : "Invoice", EntityRef = entityRef,
                Counterparty = counterparty, BilledAmount = billed, ExpectedAmount = expected,
                RecoverableAmount = Math.Max(0, Math.Round(recoverable, 2)), Confidence = confidence,
                Evidence = evidence ?? [],
            };

            if (contract is not null && (inv.ServicePeriodStart < contract.EffectiveFrom ||
                                         (contract.EffectiveTo is { } to && inv.ServicePeriodEnd > to)))
                findings.Add(Make("WH-PERIOD", Severity.Medium, inv.InvoiceNumber,
                    $"Invoice {inv.InvoiceNumber} bills outside contract {contract.ContractId} term",
                    $"Service period {inv.ServicePeriodStart:yyyy-MM-dd} to {inv.ServicePeriodEnd:yyyy-MM-dd} is outside the contract term " +
                    $"{contract.EffectiveFrom:yyyy-MM-dd} to {contract.EffectiveTo?.ToString("yyyy-MM-dd") ?? "open"}. Confirm which rate card applies.",
                    0, 0.7));

            var lineSum = inv.Lines.Sum(l => l.Amount);
            if (inv.InvoiceTotal is { } total && total - lineSum > s.RateToleranceAmount)
                findings.Add(Make("WH-TOTAL", Severity.High, inv.InvoiceNumber,
                    $"Invoice {inv.InvoiceNumber} total exceeds its lines",
                    $"Invoice total {total:C} but lines sum to {lineSum:C}.", total - lineSum, 0.97, total, lineSum));

            foreach (var line in inv.Lines)
            {
                var lineRef = $"{inv.InvoiceNumber}#{line.LineId}";
                if (s.MinimumChargeCodes.Contains(line.ChargeCode)) continue;

                var rate = contract?.Rates.FirstOrDefault(r => r.ChargeCode.Equals(line.ChargeCode, StringComparison.OrdinalIgnoreCase));
                if (contract is not null && rate is null)
                {
                    findings.Add(Make("WH-UNCONTRACTED", Severity.High, lineRef,
                        $"Uncontracted charge '{line.ChargeCode}' on {inv.InvoiceNumber}",
                        $"Charge code '{line.ChargeCode}' ({line.Description}) is not in contract {contract.ContractId}. " +
                        "Unless there is a signed amendment or change order, it is not payable.",
                        line.Amount, 0.8, line.Amount, 0));
                    continue;
                }

                var computed = line.Quantity * line.UnitRate;
                var floor = Math.Max(computed, rate?.LineMinimum ?? 0);
                if (line.Amount - floor > s.RateToleranceAmount)
                    findings.Add(Make("WH-MATH", Severity.High, lineRef,
                        $"Extension error on {lineRef}",
                        $"{line.Quantity} × {line.UnitRate:C} = {computed:C}" +
                        (rate?.LineMinimum is { } m ? $" (line minimum {m:C})" : "") + $", but the line bills {line.Amount:C}.",
                        line.Amount - floor, 0.97, line.Amount, floor));

                if (rate is not null && line.UnitRate - rate.Rate > s.RateToleranceAmount)
                    findings.Add(Make("WH-RATE", Severity.High, lineRef,
                        $"Rate above contract for '{line.ChargeCode}' on {inv.InvoiceNumber}",
                        $"Billed {line.UnitRate:C}/{rate.Unit ?? "unit"}; contract {contract!.ContractId} rate is {rate.Rate:C}.",
                        (line.UnitRate - rate.Rate) * line.Quantity, 0.95, line.Amount, rate.Rate * line.Quantity,
                        new() { ["contractRate"] = rate.Rate.ToString(), ["billedRate"] = line.UnitRate.ToString() }));

                if (rate is { RequiresApproval: true } && string.IsNullOrWhiteSpace(line.ApprovalReference))
                    findings.Add(Make("WH-APPROVAL", Severity.Medium, lineRef,
                        $"Unapproved '{line.ChargeCode}' charge on {inv.InvoiceNumber}",
                        "The contract requires client approval for this charge, and the line carries no approval reference.",
                        line.Amount, 0.75, line.Amount, 0));
            }

            if (contract?.MonthlyMinimum is { } minimum)
                findings.AddRange(CheckMinimum(inv, minimum, s, Make));
        }

        findings.AddRange(CheckDuplicates(p));
        findings.AddRange(CheckQuantities(p, s));
        return findings;
    }

    private static IEnumerable<Finding> CheckMinimum(WarehouseInvoice inv, decimal minimum, AuditSettings s,
        Func<string, Severity, string, string, string, decimal, double, decimal?, decimal?, Dictionary<string, string>?, Finding> make)
    {
        var minLines = inv.Lines.Where(l => s.MinimumChargeCodes.Contains(l.ChargeCode)).ToList();
        if (minLines.Count == 0) yield break;
        var activity = inv.Lines.Where(l => !s.MinimumChargeCodes.Contains(l.ChargeCode)).Sum(l => l.Amount);
        var billedTopUp = minLines.Sum(l => l.Amount);
        var allowedTopUp = Math.Max(0, minimum - activity);
        if (billedTopUp - allowedTopUp > s.RateToleranceAmount)
            yield return make("WH-MINIMUM", Severity.High, inv.InvoiceNumber,
                $"Monthly minimum over-applied on {inv.InvoiceNumber}",
                $"Minimum is {minimum:C} and activity charges are {activity:C}, so at most {allowedTopUp:C} top-up is owed; {billedTopUp:C} was billed.",
                billedTopUp - allowedTopUp, 0.9, billedTopUp, allowedTopUp, null);
    }

    private static IEnumerable<Finding> CheckDuplicates(AuditPackage p)
    {
        var lines = p.WarehouseInvoices.SelectMany(inv => inv.Lines.Select(l => (inv, l))).ToList();

        // Same charge against the same order/receipt reference.
        var byRef = lines.Where(x => !string.IsNullOrWhiteSpace(x.l.Reference))
                         .GroupBy(x => (Code: x.l.ChargeCode.ToUpperInvariant(), Reference: x.l.Reference!, x.l.Quantity, x.l.Amount));
        // Period-based charges (storage, management fees) without a reference: same period billed on two invoices.
        var byPeriod = lines.Where(x => string.IsNullOrWhiteSpace(x.l.Reference))
                            .GroupBy(x => (Code: x.l.ChargeCode.ToUpperInvariant(), Reference: $"{x.inv.FacilityId}:{x.inv.ServicePeriodStart}:{x.inv.ServicePeriodEnd}", x.l.Quantity, x.l.Amount))
                            .Where(g => g.Select(x => x.inv.InvoiceNumber).Distinct().Count() > 1);

        foreach (var g in byRef.Concat(byPeriod).Where(g => g.Count() > 1))
        {
            var first = g.First();
            var dupes = g.Skip(1).ToList();
            var refs = string.Join(", ", g.Select(x => $"{x.inv.InvoiceNumber}#{x.l.LineId}"));
            var entity = $"{dupes[0].inv.InvoiceNumber}#{dupes[0].l.LineId}";
            yield return new Finding
            {
                Id = Finding.MakeId("WH-DUP", $"{p.PackageId}|{refs}"),
                RuleId = "WH-DUP", Category = AuditCategory.WarehouseBilling, Severity = Severity.High,
                Title = $"Duplicate '{first.l.ChargeCode}' charge",
                Detail = $"The same charge ({first.l.Quantity} × {first.l.ChargeCode}, {first.l.Amount:C}, reference '{g.Key.Reference}') appears {g.Count()} times: {refs}.",
                EntityType = "InvoiceLine", EntityRef = entity,
                Counterparty = FindContract(p, first.inv)?.ThreePLName,
                BilledAmount = g.Sum(x => x.l.Amount), ExpectedAmount = first.l.Amount,
                RecoverableAmount = dupes.Sum(x => x.l.Amount), Confidence = 0.9,
            };
        }
    }

    private static IEnumerable<Finding> CheckQuantities(AuditPackage p, AuditSettings s)
    {
        if (p.Activity.Count == 0) yield break;

        var billed = p.WarehouseInvoices
            .SelectMany(inv => inv.Lines.Select(l => (inv, l)))
            .Where(x => !s.MinimumChargeCodes.Contains(x.l.ChargeCode))
            .GroupBy(x => (Code: x.l.ChargeCode.ToUpperInvariant(), x.inv.FacilityId, x.inv.ServicePeriodStart, x.inv.ServicePeriodEnd));

        foreach (var g in billed)
        {
            var activity = p.Activity.Where(a =>
                a.ChargeCode.Equals(g.Key.Code, StringComparison.OrdinalIgnoreCase)
                && (a.FacilityId is null || g.Key.FacilityId is null || a.FacilityId == g.Key.FacilityId)
                && a.PeriodStart >= g.Key.ServicePeriodStart && a.PeriodEnd <= g.Key.ServicePeriodEnd).ToList();
            if (activity.Count == 0) continue;

            var billedQty = g.Sum(x => x.l.Quantity);
            var actualQty = activity.Sum(a => a.Quantity);
            if (billedQty <= actualQty * (1 + s.QuantityTolerancePercent / 100m)) continue;

            var avgRate = g.Sum(x => x.l.Amount) / billedQty;
            var invoices = string.Join(", ", g.Select(x => x.inv.InvoiceNumber).Distinct());
            var entity = $"{g.Key.Code}@{g.Key.FacilityId}:{g.Key.ServicePeriodStart:yyyy-MM-dd}";
            yield return new Finding
            {
                Id = Finding.MakeId("WH-QTY", $"{p.PackageId}|{entity}"),
                RuleId = "WH-QTY", Category = AuditCategory.WarehouseBilling, Severity = Severity.High,
                Title = $"Billed {g.Key.Code} quantity exceeds recorded activity",
                Detail = $"Invoices {invoices} bill {billedQty:N2} units of {g.Key.Code} for {g.Key.ServicePeriodStart:yyyy-MM-dd} to {g.Key.ServicePeriodEnd:yyyy-MM-dd}; " +
                         $"client records ({string.Join(", ", activity.Select(a => a.Source ?? "activity").Distinct())}) show {actualQty:N2}.",
                EntityType = "ChargeCodePeriod", EntityRef = entity,
                Counterparty = FindContract(p, g.First().inv)?.ThreePLName,
                BilledAmount = g.Sum(x => x.l.Amount), ExpectedAmount = Math.Round(actualQty * avgRate, 2),
                RecoverableAmount = Math.Round((billedQty - actualQty) * avgRate, 2),
                Confidence = 0.85,
                Evidence = new() { ["billedQuantity"] = billedQty.ToString(), ["activityQuantity"] = actualQty.ToString() },
            };
        }
    }

    private static WarehouseContract? FindContract(AuditPackage p, WarehouseInvoice inv) =>
        inv.ContractId is { } id
            ? p.WarehouseContracts.FirstOrDefault(c => c.ContractId == id)
            : p.WarehouseContracts.Count == 1 ? p.WarehouseContracts[0] : null;
}
