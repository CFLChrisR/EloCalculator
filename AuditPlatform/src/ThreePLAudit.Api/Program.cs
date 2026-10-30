using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ThreePLAudit.Core;
using ThreePLAudit.Core.Auditing;
using ThreePLAudit.Core.Intake;
using ThreePLAudit.Core.Regulations;
using ThreePLAudit.Data;

// Money in findings and dispute letters is formatted in US dollars regardless of server locale.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connection = builder.Configuration.GetConnectionString("Audit") ?? "Data Source=audit.db";
builder.Services.AddDbContext<AuditDbContext>(o =>
{
    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)) o.UseSqlServer(connection);
    else o.UseSqlite(connection);
});
builder.Services.AddScoped<AuditStore>();
builder.Services.AddSingleton(sp => builder.Configuration.GetSection("Audit").Get<AuditSettings>() ?? new AuditSettings());
builder.Services.AddSingleton(sp => AuditEngine.CreateDefault(sp.GetRequiredService<AuditSettings>()));
builder.Services.AddHttpClient<SourceVerifier>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("ThreePLAudit-SourceVerifier/0.1");
});

var app = builder.Build();

// Create the schema and seed the reference library. The seed only adds missing entries,
// so edits and verifications made in the app are never overwritten.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    db.Database.EnsureCreated();
    var seedDir = Path.Combine(AppContext.BaseDirectory, "data", "regulations");
    if (Directory.Exists(seedDir))
        await scope.ServiceProvider.GetRequiredService<AuditStore>().SeedLibraryAsync(RegulationLibrary.LoadDirectory(seedDir));
}

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

// ---------------------------------------------------------------- packages (intake hands off here)

api.MapPost("/packages", async (AuditPackage package, AuditStore store) =>
{
    var issues = PackageValidator.Validate(package);
    if (issues.Any(i => i.IsError)) return Results.UnprocessableEntity(new { issues });
    await store.SavePackageAsync(package);
    return Results.Ok(new { package.PackageId, issues });
});

api.MapGet("/packages", async (AuditStore store) =>
    (await store.ListPackagesAsync()).Select(p => new { p.Id, p.ClientName, p.PeriodStart, p.PeriodEnd, p.ReceivedAt }));

api.MapGet("/packages/{id}", async (string id, AuditStore store) =>
    await store.GetPackageAsync(id) is { } p ? Results.Ok(p) : Results.NotFound());

// Intake can submit and audit in one call: this is the no-human path.
api.MapPost("/packages/{id}/audit", async (string id, AuditStore store, AuditEngine engine) =>
{
    var package = await store.GetPackageAsync(id);
    if (package is null) return Results.NotFound();
    var run = engine.Run(package, await store.LoadLibraryAsync());
    if (run.ValidationIssues.Any(i => i.IsError)) return Results.UnprocessableEntity(new { run.ValidationIssues });
    await store.SaveRunAsync(run);
    return Results.Ok(run);
});

api.MapPost("/audit", async (AuditPackage package, AuditStore store, AuditEngine engine) =>
{
    var run = engine.Run(package, await store.LoadLibraryAsync());
    if (run.ValidationIssues.Any(i => i.IsError)) return Results.UnprocessableEntity(new { run.ValidationIssues });
    await store.SavePackageAsync(package);
    await store.SaveRunAsync(run);
    return Results.Ok(run);
});

// ---------------------------------------------------------------- runs & findings

api.MapGet("/rules", (AuditEngine engine) =>
    engine.Modules.Select(m => new { category = m.Category, rules = m.Rules }));

api.MapGet("/settings", (AuditSettings s) => s);

api.MapGet("/runs", async (string? packageId, AuditStore store) =>
    (await store.ListRunsAsync(packageId)).Select(r => new { r.Id, r.PackageId, r.ClientName, r.RunAt, r.TotalRecoverable, r.FindingCount }));

api.MapGet("/runs/{runId}", async (string runId, AuditStore store) =>
{
    var run = await store.GetRunAsync(runId);
    if (run is null) return Results.NotFound();
    var findings = await store.QueryFindingsAsync(new FindingQuery(RunId: runId));
    return Results.Ok(new
    {
        run.Id, run.PackageId, run.ClientName, run.RunAt,
        summary = System.Text.Json.JsonSerializer.Deserialize<AuditSummary>(run.SummaryJson, AuditJson.Options),
        validationIssues = System.Text.Json.JsonSerializer.Deserialize<List<ValidationIssue>>(run.ValidationJson, AuditJson.Options),
        findings,
    });
});

api.MapGet("/findings", async (string? runId, string? packageId, string? category, string? disposition,
                               string? reviewStatus, AuditStore store) =>
    await store.QueryFindingsAsync(new FindingQuery(runId, packageId,
        ParseEnum<AuditCategory>(category), ParseEnum<Disposition>(disposition), ParseEnum<ReviewStatus>(reviewStatus))));

api.MapPatch("/runs/{runId}/findings/{findingId}", async (string runId, string findingId, ReviewRequest req, AuditStore store) =>
    await store.ReviewAsync(runId, findingId, req.Status, req.Note, req.ReviewedBy, req.RecoveredAmount) is { } v
        ? Results.Ok(v) : Results.NotFound());

api.MapGet("/runs/{runId}/findings/{findingId}/dispute", async (string runId, string findingId, AuditStore store) =>
{
    var view = await store.GetFindingAsync(runId, findingId);
    if (view is null) return Results.NotFound();
    var run = await store.GetRunAsync(runId);
    return Results.Ok(DisputeDrafter.Draft(view.Finding, run?.ClientName ?? "client", await store.LoadLibraryAsync(),
                                           DateOnly.FromDateTime(DateTime.UtcNow)));
});

api.MapGet("/runs/{runId}/export.csv", async (string runId, AuditStore store) =>
    Results.Text(ReportWriter.ToCsv((await store.QueryFindingsAsync(new FindingQuery(RunId: runId))).Select(v => v.Finding)),
                 "text/csv"));

// ---------------------------------------------------------------- reference library

api.MapGet("/jurisdictions", () =>
    new[] { Jurisdictions.Federal, Jurisdictions.ModelOrIndustry }.Concat(Jurisdictions.States.Keys)
        .Select(code => new { code, name = Jurisdictions.Name(code) }));

api.MapGet("/topics", () => Topics.All);

api.MapGet("/regulations", async (string? jurisdiction, string? topic, string? q, bool? includeFederal, AuditStore store) =>
    (await store.LoadLibraryAsync())
        .Query(jurisdiction, topic, q, includeFederal ?? true, includeFederal ?? true)
        .Select(r => new { reference = r, status = r.Status }));

api.MapGet("/regulations/coverage", async (AuditStore store) => (await store.LoadLibraryAsync()).Coverage());

api.MapGet("/regulations/{id}", async (string id, AuditStore store) =>
    (await store.LoadLibraryAsync()).Get(id) is { } r
        ? Results.Ok(new { reference = r, status = r.Status, history = await store.SourceHistoryAsync(id) })
        : Results.NotFound());

api.MapPut("/regulations/{id}", async (string id, RegulationReference r, AuditStore store) =>
{
    if (!r.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) return Results.BadRequest("Id in the URL and body must match.");
    var j = r.Jurisdiction.ToUpperInvariant();
    if (j != Jurisdictions.Federal && j != Jurisdictions.ModelOrIndustry && !Jurisdictions.IsState(j))
        return Results.BadRequest($"Unknown jurisdiction '{r.Jurisdiction}'.");
    var existing = (await store.LoadLibraryAsync()).Get(id);
    // Editing the source or claim clears the human verification, because it no longer applies.
    var keepVerification = existing is not null && existing.SourceUrl == r.SourceUrl && existing.Citation == r.Citation
                           && existing.Summary == r.Summary
                           && System.Text.Json.JsonSerializer.Serialize(existing.Requirement, AuditJson.Options)
                              == System.Text.Json.JsonSerializer.Serialize(r.Requirement, AuditJson.Options);
    var verification = existing is null ? new VerificationRecord()
        : keepVerification ? existing.Verification
        : existing.Verification with { VerifiedBy = null, VerifiedAt = null, VerifiedExcerpt = null, VerifiedContentSha256 = null };
    var toSave = r with { Verification = verification };
    await store.UpsertRegulationAsync(toSave);
    return Results.Ok(new { reference = toSave, status = toSave.Status });
});

api.MapDelete("/regulations/{id}", async (string id, AuditStore store) =>
    await store.DeleteRegulationAsync(id) ? Results.NoContent() : Results.NotFound());

api.MapPost("/regulations/{id}/check", async (string id, AuditStore store, SourceVerifier verifier) =>
{
    var r = (await store.LoadLibraryAsync()).Get(id);
    if (r is null) return Results.NotFound();
    var checkedRef = await verifier.CheckAsync(r);
    await store.RecordSourceCheckAsync(checkedRef);
    return Results.Ok(new { reference = checkedRef, status = checkedRef.Status });
});

api.MapPost("/regulations/check-all", async (string? jurisdiction, AuditStore store, SourceVerifier verifier) =>
{
    var lib = await store.LoadLibraryAsync();
    var targets = lib.All.Where(r => r.SourceUrl is not null &&
                                     (jurisdiction is null || r.Jurisdiction.Equals(jurisdiction, StringComparison.OrdinalIgnoreCase)));
    var results = await verifier.CheckAllAsync(targets);
    foreach (var r in results) await store.RecordSourceCheckAsync(r);
    return Results.Ok(results.GroupBy(r => r.Status).ToDictionary(g => g.Key.ToString(), g => g.Count()));
});

api.MapPost("/regulations/{id}/confirm", async (string id, ConfirmRequest req, AuditStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.VerifiedBy)) return Results.BadRequest("verifiedBy is required.");
    var r = (await store.LoadLibraryAsync()).Get(id);
    if (r is null) return Results.NotFound();
    if (r.SourceUrl is null) return Results.BadRequest("Add a source URL before confirming.");
    var confirmed = SourceVerifier.Confirm(r, req.VerifiedBy, req.Excerpt, DateTimeOffset.UtcNow);
    await store.UpsertRegulationAsync(confirmed);
    return Results.Ok(new { reference = confirmed, status = confirmed.Status });
});

app.Run();

static T? ParseEnum<T>(string? value) where T : struct, Enum =>
    Enum.TryParse<T>(value, ignoreCase: true, out var v) ? v : null;

public sealed record ReviewRequest(ReviewStatus Status, string? Note, string? ReviewedBy, decimal? RecoveredAmount);
public sealed record ConfirmRequest(string VerifiedBy, string? Excerpt);

public partial class Program;
