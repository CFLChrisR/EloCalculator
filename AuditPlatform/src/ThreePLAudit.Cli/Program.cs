using System.Globalization;
using System.Text.Json;
using ThreePLAudit.Core;
using ThreePLAudit.Core.Auditing;
using ThreePLAudit.Core.Intake;
using ThreePLAudit.Core.Regulations;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");
CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

const string Usage = """
    threepl-audit <command> [options]

      audit <package.json>      Run every audit rule against an intake package. No review step.
          --regs <dir>          Reference library directory (default: nearest data/regulations)
          --settings <json>     AuditSettings overrides (thresholds, tolerances)
          --out <file.json>     Write the full run result
          --csv <file.csv>      Write findings as CSV
          --disputes <dir>      Write one dispute letter per auto-dispute finding

      regs                      Search the reference library
          --state <XX>          Jurisdiction (includes federal and model entries)
          --topic <topic>       One of: {topics}
          --q <text>            Free-text search

      coverage                  Show which state/topic cells have entries, and how many are verified

      verify-sources            Fetch every source URL, record status + content hash, check phrases
          --state <XX>          Only this jurisdiction

      confirm <id> --by <name> [--excerpt <text>]
                                Record that you read the source and it supports the entry

    Exit code for audit: 0 = no findings, 2 = findings, 1 = error.
    """;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine(Usage.Replace("{topics}", string.Join(", ", Topics.All)));
    return 0;
}

var opts = ParseOptions(args.Skip(1).ToArray(), out var positional);
var regsDir = opts.GetValueOrDefault("regs") ?? FindRegsDir();
if (regsDir is null || !Directory.Exists(regsDir))
{
    Console.Error.WriteLine("Could not find the reference library. Pass --regs <dir>.");
    return 1;
}
var library = RegulationLibrary.LoadDirectory(regsDir);

try
{
    return args[0] switch
    {
        "audit" => Audit(),
        "regs" => Regs(),
        "coverage" => Coverage(),
        "verify-sources" => await VerifySources(),
        "confirm" => Confirm(),
        _ => Fail($"Unknown command '{args[0]}'.\n\n{Usage}"),
    };
}
catch (Exception ex) when (ex is IOException or JsonException or ArgumentException)
{
    return Fail(ex.Message);
}

int Audit()
{
    if (positional.Count == 0) return Fail("audit needs a package file.");
    var package = JsonSerializer.Deserialize<AuditPackage>(File.ReadAllText(positional[0]), AuditJson.Options)
                  ?? throw new JsonException("Package file is empty.");
    var settings = opts.TryGetValue("settings", out var sf)
        ? JsonSerializer.Deserialize<AuditSettings>(File.ReadAllText(sf), AuditJson.Options) ?? new AuditSettings()
        : new AuditSettings();

    var run = AuditEngine.CreateDefault(settings).Run(package, library);
    foreach (var i in run.ValidationIssues)
        Console.Error.WriteLine($"{(i.IsError ? "ERROR" : "warn ")} {i.Path}: {i.Message}");
    if (run.ValidationIssues.Any(i => i.IsError)) return 1;

    var s = run.Summary;
    Console.WriteLine($"{run.ClientName} / {run.PackageId}  ({package.PeriodStart:yyyy-MM-dd} to {package.PeriodEnd:yyyy-MM-dd})");
    Console.WriteLine($"Audited: warehouse {s.WarehouseBilledAudited:C}, freight {s.FreightBilledAudited:C}, deductions {s.DeductionsAudited:C}");
    Console.WriteLine($"Findings: {s.FindingCount}   Recoverable: {s.TotalRecoverable:C}");
    Console.WriteLine($"Auto-dispute: {s.AutoDisputeCount} for {s.AutoDisputeAmount:C}" +
                      (s.EarliestDeadline is { } d ? $" (earliest deadline {d:yyyy-MM-dd})" : ""));
    Console.WriteLine($"Needs review: {s.NeedsReviewCount}");
    Console.WriteLine();
    foreach (var f in run.Findings)
        Console.WriteLine($"  [{f.Disposition,-13}] {f.Severity,-8} {f.RuleId,-18} {f.RecoverableAmount,12:C}  {f.Title}");

    if (opts.TryGetValue("out", out var outFile)) File.WriteAllText(outFile, JsonSerializer.Serialize(run, AuditJson.Options));
    if (opts.TryGetValue("csv", out var csvFile)) File.WriteAllText(csvFile, ReportWriter.ToCsv(run.Findings));
    if (opts.TryGetValue("disputes", out var dir))
    {
        Directory.CreateDirectory(dir);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var f in run.Findings.Where(f => f.Disposition == Disposition.AutoDispute))
        {
            var draft = DisputeDrafter.Draft(f, run.ClientName, library, today);
            var text = $"Subject: {draft.Subject}\n" +
                       (draft.Warnings.Count > 0 ? string.Join("\n", draft.Warnings.Select(w => $"WARNING: {w}")) + "\n" : "") +
                       $"\n{draft.Body}";
            File.WriteAllText(Path.Combine(dir, $"{f.Id}.txt"), text);
        }
    }
    return run.Findings.Count == 0 ? 0 : 2;
}

int Regs()
{
    var results = library.Query(opts.GetValueOrDefault("state"), opts.GetValueOrDefault("topic"), opts.GetValueOrDefault("q"));
    foreach (var r in results)
    {
        Console.WriteLine($"{r.Id}  [{r.Jurisdiction}] [{r.Status}]");
        Console.WriteLine($"  {r.Citation}: {r.Title}");
        Console.WriteLine($"  {r.SourceUrl ?? "(no source yet)"}");
    }
    Console.WriteLine($"{results.Count} entries.");
    return 0;
}

int Coverage()
{
    var cells = library.Coverage();
    var topics = Topics.StateCoverage;
    Console.WriteLine("State  " + string.Join("  ", topics.Select(t => t.PadRight(24))));
    foreach (var state in cells.GroupBy(c => c.Jurisdiction))
        Console.WriteLine($"{state.Key,-5}  " + string.Join("  ", topics.Select(t =>
        {
            var c = state.First(x => x.Topic == t);
            return (c.Entries == 0 ? "-" : $"{c.Entries} ({c.Verified} verified)").PadRight(24);
        })));
    return 0;
}

async Task<int> VerifySources()
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("ThreePLAudit-SourceVerifier/0.1");
    var verifier = new SourceVerifier(http);
    var state = opts.GetValueOrDefault("state");
    var targets = library.All.Where(r => r.SourceUrl is not null &&
                                         (state is null || r.Jurisdiction.Equals(state, StringComparison.OrdinalIgnoreCase))).ToList();
    Console.WriteLine($"Checking {targets.Count} sources...");
    foreach (var r in await verifier.CheckAllAsync(targets))
    {
        library.Upsert(r);
        Console.WriteLine($"  {r.Status,-20} {r.Id,-28} {r.Verification.LastError ?? r.Verification.LastHttpStatus?.ToString()}");
    }
    library.SaveDirectory(regsDir);
    Console.WriteLine($"Updated {regsDir}.");
    return 0;
}

int Confirm()
{
    if (positional.Count == 0 || !opts.TryGetValue("by", out var by)) return Fail("confirm <id> --by <name> [--excerpt <text>]");
    var r = library.Get(positional[0]);
    if (r is null) return Fail($"No entry '{positional[0]}'.");
    if (r.Verification.LastContentSha256 is null)
        Console.Error.WriteLine("Note: no automated check on record. Run verify-sources first so later page changes are detected.");
    library.Upsert(SourceVerifier.Confirm(r, by, opts.GetValueOrDefault("excerpt"), DateTimeOffset.UtcNow));
    library.SaveDirectory(regsDir);
    Console.WriteLine($"{r.Id} confirmed by {by}.");
    return 0;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

static Dictionary<string, string> ParseOptions(string[] a, out List<string> positional)
{
    var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    positional = [];
    for (var i = 0; i < a.Length; i++)
        if (a[i].StartsWith("--", StringComparison.Ordinal) && i + 1 < a.Length) d[a[i][2..]] = a[++i];
        else positional.Add(a[i]);
    return d;
}

static string? FindRegsDir()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "data", "regulations");
            if (Directory.Exists(candidate)) return candidate;
        }
    return null;
}
