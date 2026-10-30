using Microsoft.EntityFrameworkCore;

namespace ThreePLAudit.Data;

public enum ReviewStatus { Open, Disputed, Recovered, Rejected, Dismissed }

public sealed class PackageEntity
{
    public required string Id { get; set; }
    public required string ClientName { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public required string Json { get; set; }
}

public sealed class AuditRunEntity
{
    public required string Id { get; set; }
    public required string PackageId { get; set; }
    public required string ClientName { get; set; }
    public DateTimeOffset RunAt { get; set; }
    public decimal TotalRecoverable { get; set; }
    public int FindingCount { get; set; }
    public required string SummaryJson { get; set; }
    public required string ValidationJson { get; set; }
}

/// <summary>One row per finding per run. Key fields are columns for querying; the full finding is kept as JSON.</summary>
public sealed class FindingEntity
{
    public long Key { get; set; }
    public required string RunId { get; set; }
    public required string FindingId { get; set; }
    public required string PackageId { get; set; }
    public required string RuleId { get; set; }
    public required string Category { get; set; }
    public required string Severity { get; set; }
    public required string Disposition { get; set; }
    public required string Title { get; set; }
    public required string EntityRef { get; set; }
    public string? Counterparty { get; set; }
    public decimal RecoverableAmount { get; set; }
    public double Confidence { get; set; }
    public DateOnly? Deadline { get; set; }
    public ReviewStatus ReviewStatus { get; set; }
    public string? ReviewNote { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public decimal? RecoveredAmount { get; set; }
    public required string Json { get; set; }
}

public sealed class RegulationEntity
{
    public required string Id { get; set; }
    public required string Jurisdiction { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public required string Json { get; set; }
}

/// <summary>Append-only history of automated source checks, so you can show when a source was last confirmed.</summary>
public sealed class SourceCheckEntity
{
    public long Key { get; set; }
    public required string RegulationId { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public int? HttpStatus { get; set; }
    public string? ContentSha256 { get; set; }
    public bool? PhraseFound { get; set; }
    public string? Error { get; set; }
}

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<PackageEntity> Packages => Set<PackageEntity>();
    public DbSet<AuditRunEntity> Runs => Set<AuditRunEntity>();
    public DbSet<FindingEntity> Findings => Set<FindingEntity>();
    public DbSet<RegulationEntity> Regulations => Set<RegulationEntity>();
    public DbSet<SourceCheckEntity> SourceChecks => Set<SourceCheckEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<PackageEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(100);
            e.Property(x => x.ClientName).HasMaxLength(200);
        });
        b.Entity<AuditRunEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.PackageId).HasMaxLength(100);
            e.Property(x => x.TotalRecoverable).HasPrecision(18, 2);
            e.HasIndex(x => x.PackageId);
        });
        b.Entity<FindingEntity>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.RunId).HasMaxLength(64);
            e.Property(x => x.FindingId).HasMaxLength(64);
            e.Property(x => x.PackageId).HasMaxLength(100);
            e.Property(x => x.RuleId).HasMaxLength(40);
            e.Property(x => x.Category).HasMaxLength(40);
            e.Property(x => x.Severity).HasMaxLength(20);
            e.Property(x => x.Disposition).HasMaxLength(20);
            e.Property(x => x.EntityRef).HasMaxLength(200);
            e.Property(x => x.Counterparty).HasMaxLength(200);
            e.Property(x => x.Title).HasMaxLength(400);
            e.Property(x => x.ReviewStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RecoverableAmount).HasPrecision(18, 2);
            e.Property(x => x.RecoveredAmount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.RunId, x.FindingId }).IsUnique();
            e.HasIndex(x => x.FindingId);
        });
        b.Entity<RegulationEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(100);
            e.Property(x => x.Jurisdiction).HasMaxLength(10);
            e.HasIndex(x => x.Jurisdiction);
        });
        b.Entity<SourceCheckEntity>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.RegulationId).HasMaxLength(100);
            e.HasIndex(x => x.RegulationId);
        });
    }
}
