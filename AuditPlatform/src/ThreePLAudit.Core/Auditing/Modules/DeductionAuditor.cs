using ThreePLAudit.Core.Intake;

namespace ThreePLAudit.Core.Auditing;

/// <summary>Retailer deductions and chargebacks matched against POD, BOL, ASN and ship-window evidence.</summary>
public sealed class DeductionAuditor : IAuditModule
{
    public AuditCategory Category => AuditCategory.Deductions;

    public IReadOnlyDictionary<string, string> Rules { get; } = new Dictionary<string, string>
    {
        ["DD-SHORTAGE"] = "Shortage deducted although the POD shows a clean, full receipt.",
        ["DD-SHORTAGE-PARTIAL"] = "Shortage deducted for more units than the POD exception supports.",
        ["DD-LATE"] = "Late-shipment chargeback although the shipment left inside the ship window.",
        ["DD-EARLY"] = "Early-shipment chargeback although the shipment left inside the ship window.",
        ["DD-ASN"] = "ASN chargeback although the ASN was sent within the retailer's deadline.",
        ["DD-LABEL"] = "Labeling chargeback on a shipment recorded as label-compliant.",
        ["DD-DUP"] = "Same deduction taken more than once.",
        ["DD-NO-EVIDENCE"] = "No shipment evidence on file for the deducted PO.",
    };

    public IEnumerable<Finding> Evaluate(AuditContext ctx)
    {
        var p = ctx.Package;
        var findings = new List<Finding>();
        // Repeat deductions are disputed as duplicates, so they skip the evidence checks.
        var duplicateCopies = new HashSet<string>();

        foreach (var g in p.Deductions.GroupBy(d => (R: d.Retailer.ToUpperInvariant(), d.PoNumber, d.Type, d.Amount))
                                      .Where(g => g.Key.PoNumber is not null && g.Count() > 1))
        {
            var later = g.OrderBy(d => d.DeductionDate).Skip(1).ToList();
            duplicateCopies.UnionWith(later.Select(d => d.DeductionId));
            foreach (var d in later)
                findings.Add(Build(ctx, d, "DD-DUP", Severity.High, $"Duplicate {d.Type} deduction on PO {d.PoNumber}",
                    $"{d.Retailer} took {g.Count()} identical {d.Type} deductions of {d.Amount:C} on PO {d.PoNumber}: {string.Join(", ", g.Select(x => x.DeductionId))}.",
                    d.Amount, 0.93, null));
        }

        foreach (var d in p.Deductions.Where(d => !duplicateCopies.Contains(d.DeductionId)))
        {
            var shipment = d.PoNumber is null ? null : p.Shipments.FirstOrDefault(s => s.PoNumber == d.PoNumber);
            if (shipment is null)
            {
                findings.Add(Build(ctx, d, "DD-NO-EVIDENCE", Severity.Medium, $"No evidence on file for {d.DeductionId}",
                    $"{d.Type} deduction of {d.Amount:C} on PO {d.PoNumber ?? "(none)"} has no matching shipment record. Pull the BOL, POD and ASN before the dispute window closes.",
                    0, 0.3, null));
                continue;
            }

            switch (d.Type)
            {
                case DeductionType.Shortage:
                    findings.AddRange(Shortage(ctx, d, shipment));
                    break;

                case DeductionType.LateShipment when shipment.ShipWindowEnd is { } end && shipment.ShipDate <= end:
                    findings.Add(Build(ctx, d, "DD-LATE", Severity.High, $"Late-ship chargeback disputable: {d.DeductionId}",
                        $"PO {d.PoNumber} shipped {shipment.ShipDate:yyyy-MM-dd} (BOL {shipment.BolNumber}), inside the window ending {end:yyyy-MM-dd}.",
                        d.Amount, 0.9, shipment));
                    break;

                case DeductionType.EarlyShipment when shipment.ShipWindowStart is { } start && shipment.ShipDate >= start:
                    findings.Add(Build(ctx, d, "DD-EARLY", Severity.High, $"Early-ship chargeback disputable: {d.DeductionId}",
                        $"PO {d.PoNumber} shipped {shipment.ShipDate:yyyy-MM-dd}, on or after the window start {start:yyyy-MM-dd}.",
                        d.Amount, 0.9, shipment));
                    break;

                case DeductionType.Asn:
                    var profile = Profile(p, d.Retailer);
                    if (profile?.AsnDeadlineHours is { } hours && shipment is { ShippedAt: { } shipped, AsnSentAt: { } sent }
                        && sent <= shipped.AddHours(hours))
                        findings.Add(Build(ctx, d, "DD-ASN", Severity.High, $"ASN chargeback disputable: {d.DeductionId}",
                            $"ASN sent {sent:yyyy-MM-dd HH:mm} UTC{sent:zzz}, {(sent - shipped).TotalHours:N1} h after shipment; {d.Retailer} allows {hours} h.",
                            d.Amount, 0.88, shipment));
                    break;

                case DeductionType.Labeling when shipment.LabelsCompliant:
                    findings.Add(Build(ctx, d, "DD-LABEL", Severity.Medium, $"Labeling chargeback may be disputable: {d.DeductionId}",
                        $"Shipment for PO {d.PoNumber} is recorded as label-compliant. Attach carton label scans or photos; the shipper's own flag alone rarely wins a labeling dispute.",
                        d.Amount, 0.6, shipment, ["MODEL-GS1-LOGISTICS-LABEL"]));
                    break;
            }
        }
        return findings;
    }

    private static IEnumerable<Finding> Shortage(AuditContext ctx, Deduction d, ShipmentEvidence s)
    {
        if (!s.PodSigned) yield break;
        var clean = string.IsNullOrWhiteSpace(s.PodExceptions);
        var podFull = s.PodUnitsReceived is null || s.UnitsShipped is null || s.PodUnitsReceived >= s.UnitsShipped;

        if (clean && podFull)
        {
            yield return Build(ctx, d, "DD-SHORTAGE", Severity.High, $"Shortage disputable: clean POD for {d.DeductionId}",
                $"PO {d.PoNumber}: {d.UnitsClaimed?.ToString("N0") ?? "?"} units claimed short ({d.Amount:C}), but the POD was signed with no exceptions" +
                (s.PodUnitsReceived is { } r ? $" for {r:N0} of {s.UnitsShipped:N0} units." : "."),
                d.Amount, 0.92, s);
            yield break;
        }

        if (s.UnitsShipped is { } shipped && s.PodUnitsReceived is { } received && d.UnitsClaimed is { } claimed && claimed > 0)
        {
            var supported = Math.Max(0, shipped - received);
            if (claimed > supported)
                yield return Build(ctx, d, "DD-SHORTAGE-PARTIAL", Severity.Medium, $"Shortage overstated on {d.DeductionId}",
                    $"POD supports {supported:N0} units short ({shipped:N0} shipped, {received:N0} received); {d.Retailer} deducted for {claimed:N0}.",
                    d.Amount * (claimed - supported) / claimed, 0.85, s);
        }
    }

    private static RetailerProfile? Profile(AuditPackage p, string retailer) =>
        p.RetailerProfiles.FirstOrDefault(r => r.Retailer.Equals(retailer, StringComparison.OrdinalIgnoreCase));

    private static Finding Build(AuditContext ctx, Deduction d, string rule, Severity sev, string title, string detail,
        decimal recoverable, double confidence, ShipmentEvidence? s, List<string>? regulations = null)
    {
        var profile = Profile(ctx.Package, d.Retailer);
        DateOnly? deadline = profile?.DisputeWindowDays is { } days ? d.DeductionDate.AddDays(days) : null;
        var evidence = new Dictionary<string, string> { ["deductionId"] = d.DeductionId, ["type"] = d.Type.ToString() };
        if (d.PoNumber is not null) evidence["po"] = d.PoNumber;
        if (s?.BolNumber is not null) evidence["bol"] = s.BolNumber;
        if (s is { DocumentRefs.Count: > 0 }) evidence["documents"] = string.Join("; ", s.DocumentRefs);

        return new Finding
        {
            Id = Finding.MakeId(rule, $"{ctx.Package.PackageId}|{d.DeductionId}"),
            RuleId = rule, Category = AuditCategory.Deductions, Severity = sev, Title = title, Detail = detail,
            EntityType = "Deduction", EntityRef = d.DeductionId, Counterparty = d.Retailer,
            BilledAmount = d.Amount, ExpectedAmount = d.Amount - Math.Round(recoverable, 2),
            RecoverableAmount = Math.Max(0, Math.Round(recoverable, 2)), Confidence = confidence,
            Deadline = deadline,
            DeadlineBasis = deadline is null
                ? $"No dispute window configured for {d.Retailer}; add it to the retailer profile from the vendor agreement or routing guide."
                : $"{profile!.DisputeWindowDays} days from deduction per {d.Retailer} profile ({profile.RoutingGuideRef ?? "client-supplied"}).",
            RegulationIds = regulations ?? [],
            Evidence = evidence,
        };
    }
}
