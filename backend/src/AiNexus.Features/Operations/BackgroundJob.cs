using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Operations;

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
public sealed record JobDto(Guid Id, string Kind, Guid SubjectId, string Label, string Status, string Stage, int Attempt, int CompletedUnits, int? TotalUnits, bool CancelRequested, string? ErrorCode, string? ErrorMessage, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string? IssueCode = null);

internal static class OperationsErrors
{
    public static readonly Error JobNotFound = Error.NotFound("job_not_found");
    public static readonly Error JobNotRetryable = Error.Conflict("job_not_retryable");
    public static readonly Error JobRetryLimit = Error.Conflict("job_retry_limit");
    public static readonly Error JobHandlerMissing = Error.Conflict("job_handler_missing");
    public static readonly Error JobActive = Error.Conflict("job_active");
    public static readonly Error JobChanged = Error.Conflict("job_changed");
    public static readonly Error InvalidAuditFilter = Error.Invalid("invalid_audit_filter");

    /// <summary>For <see cref="JobService"/>, whose callers in other modules can only fail by exception.</summary>
    public static ApiException ToException(this Error error)
    {
        var status = Problems.Status(error.Kind);
        return new(status, error.Code, PublicErrorCatalog.Message(error.Code, status));
    }
}

internal static class BackgroundJobQueries
{
    public static IQueryable<BackgroundJob> OwnedBy(this IQueryable<BackgroundJob> jobs, Guid owner) => jobs.Where(x => x.OwnerId == owner);

    /// <summary>Re-reads a job after a bulk update; tracked entities are discarded first.</summary>
    public static async Task<Result<JobDto>> ReloadJobAsync(this NexusDbContext db, Guid owner, Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var job = await db.Set<BackgroundJob>().OwnedBy(owner).SingleOrDefaultAsync(x => x.Id == id, ct);
        return job is null ? OperationsErrors.JobNotFound : JobService.Describe(job);
    }
}

internal sealed class BackgroundJobEntityConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> job)
    {
        job.ToTable("BackgroundJobs", "operations"); job.HasKey(x => x.Id);
        job.Property(x => x.Kind).HasMaxLength(32); job.Property(x => x.Label).HasMaxLength(180); job.Property(x => x.Status).HasMaxLength(16); job.Property(x => x.Stage).HasMaxLength(120);
        job.Property(x => x.IssueCode).HasMaxLength(40); job.Property(x => x.TraceId).HasMaxLength(32); job.Property(x => x.ParentSpanId).HasMaxLength(16);
        job.Property(x => x.ActiveKey).HasMaxLength(100); job.Property(x => x.ErrorCode).HasMaxLength(80); job.Property(x => x.ErrorMessage).HasMaxLength(240);
        job.HasIndex(x => x.ActiveKey).IsUnique().HasFilter("[ActiveKey] IS NOT NULL"); job.HasIndex(x => new { x.Status, x.LeaseUntil, x.CreatedAt }); job.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        job.HasIndex(x => x.SubjectId); // Active work on a document or embedding profile.
        job.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        job.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>The module's table, applied by <c>NexusDbContext</c>.</summary>
public static class BackgroundJobConfiguration
{
    public static void Configure(ModelBuilder model) => model.ApplyConfiguration(new BackgroundJobEntityConfiguration());
}
