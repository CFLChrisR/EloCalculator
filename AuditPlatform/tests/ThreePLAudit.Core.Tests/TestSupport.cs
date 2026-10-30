using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ThreePLAudit.Core;
using ThreePLAudit.Core.Auditing;
using ThreePLAudit.Core.Intake;
using ThreePLAudit.Core.Regulations;

namespace ThreePLAudit.Core.Tests;

internal static class TestSupport
{
    public static readonly DateOnly Today = new(2026, 10, 30);
    public static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    [ModuleInitializer]
    internal static void UseUsCulture() =>
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

    public static AuditPackage Sample() =>
        JsonSerializer.Deserialize<AuditPackage>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "sample-package.json")), AuditJson.Options)!;

    public static RegulationLibrary SeedLibrary() =>
        RegulationLibrary.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "TestData", "regulations"));

    public static AuditPackage Empty() => new() { PackageId = "P1", ClientName = "Client", PeriodStart = new(2026, 9, 1), PeriodEnd = new(2026, 9, 30) };

    public static List<Finding> Run(IAuditModule module, AuditPackage package, RegulationLibrary? library = null, AuditSettings? settings = null) =>
        module.Evaluate(new AuditContext(package, settings ?? new AuditSettings(), library ?? new RegulationLibrary(), Today)).ToList();
}
