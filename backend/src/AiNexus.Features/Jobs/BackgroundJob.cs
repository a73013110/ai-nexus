using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Jobs;

public sealed class BackgroundJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string? TraceId { get; set; }
    public string? ParentSpanId { get; set; }
    public Guid OperationId { get; set; }
    public Guid? ResourceId { get; set; }
    public Guid SubjectId { get; set; }
    public string Kind { get; set; } = "";
    public string Label { get; set; } = "";
    public string Status { get; set; } = "queued";
    public string Stage { get; set; } = "等待處理";
    public string? ActiveKey { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public bool CancelRequested { get; set; }
    public int Attempt { get; set; }
    public int CompletedUnits { get; set; }
    public int? TotalUnits { get; set; }
    public string? IssueCode { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> job)
    {
        job.ToTable("BackgroundJobs", "operations"); job.HasKey(x => x.Id);
        job.Property(x => x.Kind).HasMaxLength(32); job.Property(x => x.Label).HasMaxLength(180); job.Property(x => x.Status).HasMaxLength(16); job.Property(x => x.Stage).HasMaxLength(120);
        job.Property(x => x.IssueCode).HasMaxLength(40); job.Property(x => x.TraceId).HasMaxLength(32); job.Property(x => x.ParentSpanId).HasMaxLength(16);
        job.Property(x => x.ActiveKey).HasMaxLength(100); job.Property(x => x.ErrorCode).HasMaxLength(80); job.Property(x => x.ErrorMessage).HasMaxLength(240);
        job.HasIndex(x => x.ActiveKey).IsUnique().HasFilter("[ActiveKey] IS NOT NULL"); job.HasIndex(x => new { x.Status, x.LeaseUntil, x.CreatedAt }); job.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        job.HasIndex(x => x.SubjectId); // Active work on a document or embedding profile.
    }
}
