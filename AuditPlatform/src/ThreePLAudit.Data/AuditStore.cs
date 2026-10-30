using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ThreePLAudit.Core;
using ThreePLAudit.Core.Auditing;
using ThreePLAudit.Core.Intake;
using ThreePLAudit.Core.Regulations;

namespace ThreePLAudit.Data;

public sealed record FindingView(Finding Finding, string RunId, ReviewStatus ReviewStatus, string? ReviewNote,
                                 string? ReviewedBy, DateTimeOffset? ReviewedAt, decimal? RecoveredAmount);

public sealed record FindingQuery(string? RunId = null, string? PackageId = null, AuditCategory? Category = null,
                                  Disposition? Disposition = null, ReviewStatus? ReviewStatus = null, int Take = 500);

/// <summary>Persistence for packages, audit runs, findings and the reference library.</summary>
public sealed class AuditStore(AuditDbContext db)
{
    private static readonly JsonSerializerOptions Json = AuditJson.Options;

    // ------------------------------------------------------------ packages

    public async Task SavePackageAsync(AuditPackage package, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(package, Json);
        var existing = await db.Packages.FindAsync([package.PackageId], ct);
        if (existing is null)
            db.Packages.Add(new PackageEntity
            {
                Id = package.PackageId, ClientName = package.ClientName, PeriodStart = package.PeriodStart,
                PeriodEnd = package.PeriodEnd, ReceivedAt = DateTimeOffset.UtcNow, Json = json,
            });
        else
        {
            existing.ClientName = package.ClientName;
            existing.PeriodStart = package.PeriodStart;
            existing.PeriodEnd = package.PeriodEnd;
            existing.ReceivedAt = DateTimeOffset.UtcNow;
            existing.Json = json;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<AuditPackage?> GetPackageAsync(string id, CancellationToken ct = default) =>
        await db.Packages.FindAsync([id], ct) is { } e ? JsonSerializer.Deserialize<AuditPackage>(e.Json, Json) : null;

    public async Task<List<PackageEntity>> ListPackagesAsync(CancellationToken ct = default) =>
        (await db.Packages.AsNoTracking().ToListAsync(ct)).OrderByDescending(p => p.ReceivedAt).ToList();

    // ------------------------------------------------------------ runs & findings

    /// <summary>
    /// Saves a run. If a finding's id already exists from an earlier run, its review state is carried
    /// forward, so re-running an audit doesn't reopen things a person already handled.
    /// </summary>
    public async Task SaveRunAsync(AuditRunResult run, CancellationToken ct = default)
    {
        db.Runs.Add(new AuditRunEntity
        {
            Id = run.RunId, PackageId = run.PackageId, ClientName = run.ClientName, RunAt = run.RunAt,
            TotalRecoverable = run.Summary.TotalRecoverable, FindingCount = run.Findings.Count,
            SummaryJson = JsonSerializer.Serialize(run.Summary, Json),
            ValidationJson = JsonSerializer.Serialize(run.ValidationIssues, Json),
        });

        var ids = run.Findings.Select(f => f.Id).ToList();
        var prior = (await db.Findings.AsNoTracking().Where(f => ids.Contains(f.FindingId)).ToListAsync(ct))
            .GroupBy(f => f.FindingId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.Key).First());

        foreach (var f in run.Findings)
        {
            prior.TryGetValue(f.Id, out var p);
            db.Findings.Add(new FindingEntity
            {
                RunId = run.RunId, FindingId = f.Id, PackageId = run.PackageId, RuleId = f.RuleId,
                Category = f.Category.ToString(), Severity = f.Severity.ToString(), Disposition = f.Disposition.ToString(),
                Title = Truncate(f.Title, 400), EntityRef = Truncate(f.EntityRef, 200), Counterparty = f.Counterparty,
                RecoverableAmount = f.RecoverableAmount, Confidence = f.Confidence, Deadline = f.Deadline,
                ReviewStatus = p?.ReviewStatus ?? (f.Disposition == Disposition.AutoDispute ? ReviewStatus.Disputed : ReviewStatus.Open),
                ReviewNote = p?.ReviewNote, ReviewedBy = p?.ReviewedBy, ReviewedAt = p?.ReviewedAt, RecoveredAmount = p?.RecoveredAmount,
                Json = JsonSerializer.Serialize(f, Json),
            });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<AuditRunEntity>> ListRunsAsync(string? packageId = null, CancellationToken ct = default)
    {
        var q = db.Runs.AsNoTracking();
        if (packageId is not null) q = q.Where(r => r.PackageId == packageId);
        return (await q.ToListAsync(ct)).OrderByDescending(r => r.RunAt).ToList();
    }

    public async Task<AuditRunEntity?> GetRunAsync(string runId, CancellationToken ct = default) =>
        await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);

    public async Task<List<FindingView>> QueryFindingsAsync(FindingQuery q, CancellationToken ct = default)
    {
        var rows = db.Findings.AsNoTracking();
        if (q.RunId is not null) rows = rows.Where(f => f.RunId == q.RunId);
        if (q.PackageId is not null) rows = rows.Where(f => f.PackageId == q.PackageId);
        if (q.Category is { } c) rows = rows.Where(f => f.Category == c.ToString());
        if (q.Disposition is { } d) rows = rows.Where(f => f.Disposition == d.ToString());
        if (q.ReviewStatus is { } s) rows = rows.Where(f => f.ReviewStatus == s);
        var list = await rows.OrderByDescending(f => f.Key).Take(q.Take).ToListAsync(ct);
        return list.OrderByDescending(f => f.RecoverableAmount).Select(ToView).ToList();
    }

    public async Task<FindingView?> GetFindingAsync(string runId, string findingId, CancellationToken ct = default) =>
        await db.Findings.AsNoTracking().FirstOrDefaultAsync(f => f.RunId == runId && f.FindingId == findingId, ct) is { } e ? ToView(e) : null;

    public async Task<FindingView?> ReviewAsync(string runId, string findingId, ReviewStatus status, string? note,
                                                string? reviewer, decimal? recovered, CancellationToken ct = default)
    {
        var e = await db.Findings.FirstOrDefaultAsync(f => f.RunId == runId && f.FindingId == findingId, ct);
        if (e is null) return null;
        e.ReviewStatus = status;
        e.ReviewNote = note;
        e.ReviewedBy = reviewer;
        e.ReviewedAt = DateTimeOffset.UtcNow;
        e.RecoveredAmount = recovered;
        await db.SaveChangesAsync(ct);
        return ToView(e);
    }

    private static FindingView ToView(FindingEntity e) => new(
        JsonSerializer.Deserialize<Finding>(e.Json, Json)!, e.RunId, e.ReviewStatus, e.ReviewNote, e.ReviewedBy, e.ReviewedAt, e.RecoveredAmount);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    // ------------------------------------------------------------ reference library

    public async Task<RegulationLibrary> LoadLibraryAsync(CancellationToken ct = default) =>
        new((await db.Regulations.AsNoTracking().ToListAsync(ct))
            .Select(e => JsonSerializer.Deserialize<RegulationReference>(e.Json, Json)!));

    public async Task UpsertRegulationAsync(RegulationReference r, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(r, Json);
        var e = await db.Regulations.FindAsync([r.Id], ct);
        if (e is null)
            db.Regulations.Add(new RegulationEntity { Id = r.Id, Jurisdiction = r.Jurisdiction.ToUpperInvariant(), UpdatedAt = DateTimeOffset.UtcNow, Json = json });
        else
        {
            e.Jurisdiction = r.Jurisdiction.ToUpperInvariant();
            e.UpdatedAt = DateTimeOffset.UtcNow;
            e.Json = json;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteRegulationAsync(string id, CancellationToken ct = default)
    {
        var e = await db.Regulations.FindAsync([id], ct);
        if (e is null) return false;
        db.Regulations.Remove(e);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task RecordSourceCheckAsync(RegulationReference checkedRef, CancellationToken ct = default)
    {
        var v = checkedRef.Verification;
        db.SourceChecks.Add(new SourceCheckEntity
        {
            RegulationId = checkedRef.Id, CheckedAt = v.LastCheckedAt ?? DateTimeOffset.UtcNow, HttpStatus = v.LastHttpStatus,
            ContentSha256 = v.LastContentSha256, PhraseFound = v.LastPhraseFound, Error = v.LastError,
        });
        await UpsertRegulationAsync(checkedRef, ct);
    }

    public async Task<List<SourceCheckEntity>> SourceHistoryAsync(string regulationId, CancellationToken ct = default) =>
        (await db.SourceChecks.AsNoTracking().Where(s => s.RegulationId == regulationId).ToListAsync(ct))
            .OrderByDescending(s => s.CheckedAt).ToList();

    /// <summary>Adds seed entries that aren't in the database yet. Never overwrites edits or verifications.</summary>
    public async Task<int> SeedLibraryAsync(RegulationLibrary seed, CancellationToken ct = default)
    {
        var existing = (await db.Regulations.Select(r => r.Id).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var r in seed.All.Where(r => !existing.Contains(r.Id)))
        {
            db.Regulations.Add(new RegulationEntity
            {
                Id = r.Id, Jurisdiction = r.Jurisdiction, UpdatedAt = DateTimeOffset.UtcNow, Json = JsonSerializer.Serialize(r, Json),
            });
            added++;
        }
        await db.SaveChangesAsync(ct);
        return added;
    }
}
