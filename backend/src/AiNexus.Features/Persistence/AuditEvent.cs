using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Persistence;

public static class AuditOutcomes
{
    // Legacy records retain their original result; filters must recognize every successful operation.
    public static readonly string[] Accepted = ["saved", "read", "completed", "success", "granted", "granted_once", "created", "deleted", "soft_deleted", "queued", "running", "cancelled", "revoked", "removed", "assigned", "read-only", "private", "started", "restored", "activated", "cleared", "connected", "csv", "metrics", "reindexing", "snapshot"];
}

public sealed class AuditEvent
{
    public long Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? ActorId { get; set; }
    public string? TraceId { get; set; }
    public Guid? OperationId { get; set; }
    public string? IssueCode { get; set; }
    public string Action { get; set; } = "";
    public Guid? ResourceId { get; set; }
    // New committed business events have an explicit outcome; stored legacy nulls remain null.
    public string? Result { get; set; } = "completed";
    public string? DetailsJson { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> audit)
    {
        audit.ToTable("AuditEvents", "operations");
        audit.HasKey(x => x.Id);
        audit.Property(x => x.Action).HasMaxLength(64);
        audit.Property(x => x.TraceId).HasMaxLength(32); audit.Property(x => x.IssueCode).HasMaxLength(40);
        audit.Property(x => x.Result).HasMaxLength(80);
        audit.Property(x => x.DetailsJson).HasMaxLength(40000);
        audit.HasIndex(x => x.At);
        audit.HasIndex(x => new { x.Action, x.Id });
        audit.HasIndex(x => new { x.ResourceId, x.Id });
        audit.HasIndex(x => new { x.ActorId, x.Id });
    }
}
