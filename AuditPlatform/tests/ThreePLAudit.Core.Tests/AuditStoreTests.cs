using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ThreePLAudit.Core.Auditing;
using ThreePLAudit.Core.Regulations;
using ThreePLAudit.Data;
using static ThreePLAudit.Core.Tests.TestSupport;

namespace ThreePLAudit.Core.Tests;

public sealed class AuditStoreTests : IDisposable
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private readonly AuditDbContext _db;
    private readonly AuditStore _store;

    public AuditStoreTests()
    {
        _conn.Open();
        _db = new AuditDbContext(new DbContextOptionsBuilder<AuditDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _store = new AuditStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    [Fact]
    public async Task Package_and_run_round_trip()
    {
        await _store.SavePackageAsync(Sample());
        var package = await _store.GetPackageAsync("ACME-2026-09");
        Assert.NotNull(package);
        Assert.Equal(Sample().FreightBills.Count, package.FreightBills.Count);

        await _store.SeedLibraryAsync(SeedLibrary());
        var run = AuditEngine.CreateDefault().Run(package, await _store.LoadLibraryAsync(), Now);
        await _store.SaveRunAsync(run);

        var findings = await _store.QueryFindingsAsync(new FindingQuery(RunId: run.RunId));
        Assert.Equal(run.Findings.Count, findings.Count);
        Assert.All(findings.Where(f => f.Finding.Disposition == Disposition.AutoDispute), f => Assert.Equal(ReviewStatus.Disputed, f.ReviewStatus));
        Assert.NotEmpty(await _store.QueryFindingsAsync(new FindingQuery(Category: AuditCategory.Freight)));
    }

    [Fact]
    public async Task Review_state_carries_forward_to_reruns()
    {
        await _store.SeedLibraryAsync(SeedLibrary());
        var lib = await _store.LoadLibraryAsync();
        var first = AuditEngine.CreateDefault().Run(Sample(), lib, Now);
        await _store.SaveRunAsync(first);
        var target = first.Findings.First(f => f.RuleId == "WH-UNCONTRACTED");
        await _store.ReviewAsync(first.RunId, target.Id, ReviewStatus.Rejected, "Signed change order CO-12", "chris", null);

        var second = AuditEngine.CreateDefault().Run(Sample(), lib, Now);
        await _store.SaveRunAsync(second);
        var carried = await _store.GetFindingAsync(second.RunId, target.Id);
        Assert.Equal(ReviewStatus.Rejected, carried!.ReviewStatus);
        Assert.Equal("Signed change order CO-12", carried.ReviewNote);
    }

    [Fact]
    public async Task Seeding_never_overwrites_edits_or_verification()
    {
        Assert.True(await _store.SeedLibraryAsync(SeedLibrary()) > 100);
        var edited = SourceVerifier.Confirm(SeedLibrary().Get("US-49USC13710")!, "chris", "180 days", Now) with { Notes = "edited" };
        await _store.UpsertRegulationAsync(edited);

        Assert.Equal(0, await _store.SeedLibraryAsync(SeedLibrary()));
        var stored = (await _store.LoadLibraryAsync()).Get("US-49USC13710")!;
        Assert.Equal("edited", stored.Notes);
        Assert.Equal("chris", stored.Verification.VerifiedBy);
    }

    [Fact]
    public async Task Source_checks_are_kept_as_history()
    {
        await _store.SeedLibraryAsync(SeedLibrary());
        var r = SeedLibrary().Get("US-49CFR370")!;
        await _store.RecordSourceCheckAsync(r with { Verification = r.Verification with { LastCheckedAt = Now, LastHttpStatus = 200, LastPhraseFound = true } });
        await _store.RecordSourceCheckAsync(r with { Verification = r.Verification with { LastCheckedAt = Now.AddDays(1), LastHttpStatus = 404, LastError = "HTTP 404" } });
        var history = await _store.SourceHistoryAsync("US-49CFR370");
        Assert.Equal(2, history.Count);
        Assert.Equal(404, history[0].HttpStatus);
        Assert.Equal(VerificationStatus.Broken, (await _store.LoadLibraryAsync()).Get("US-49CFR370")!.Status);
    }
}
