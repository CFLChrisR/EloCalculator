namespace ThreePLAudit.Core.Intake;

public sealed record ValidationIssue(string Path, string Message, bool IsError);

/// <summary>
/// Structural checks on an incoming package. Errors block an audit; warnings mean some rules
/// will have nothing to test against.
/// </summary>
public static class PackageValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(AuditPackage package)
    {
        var issues = new List<ValidationIssue>();
        void Error(string path, string msg) => issues.Add(new(path, msg, true));
        void Warn(string path, string msg) => issues.Add(new(path, msg, false));

        if (string.IsNullOrWhiteSpace(package.PackageId)) Error("packageId", "Required.");
        if (string.IsNullOrWhiteSpace(package.ClientName)) Error("clientName", "Required.");
        if (package.PeriodEnd < package.PeriodStart) Error("periodEnd", "Period end is before period start.");

        foreach (var f in package.Facilities)
            if (!Jurisdictions.IsState(f.State))
                Error($"facilities[{f.FacilityId}].state", $"'{f.State}' is not a US state code.");

        foreach (var dup in package.WarehouseInvoices.GroupBy(i => i.InvoiceNumber).Where(g => g.Count() > 1))
            Warn($"warehouseInvoices[{dup.Key}]", "Invoice number appears more than once in the package.");

        foreach (var inv in package.WarehouseInvoices)
        {
            if (inv.Lines.Count == 0) Warn($"warehouseInvoices[{inv.InvoiceNumber}]", "Invoice has no lines.");
            if (inv.ContractId is { } cid && package.WarehouseContracts.All(c => c.ContractId != cid))
                Error($"warehouseInvoices[{inv.InvoiceNumber}].contractId", $"Contract '{cid}' is not in the package.");
        }

        if (package.WarehouseInvoices.Count > 0 && package.WarehouseContracts.Count == 0)
            Warn("warehouseContracts", "Invoices supplied without a contract; rate checks will be skipped.");
        if (package.WarehouseInvoices.Count > 0 && package.Activity.Count == 0)
            Warn("activity", "No activity data; quantity checks will be skipped.");
        if (package.FreightBills.Count > 0 && package.CarrierContracts.Count == 0)
            Warn("carrierContracts", "Freight bills supplied without carrier contracts; rate checks will be skipped.");
        if (package.Deductions.Count > 0 && package.Shipments.Count == 0)
            Warn("shipments", "Deductions supplied without shipment evidence; disputes cannot be supported.");

        return issues;
    }
}
