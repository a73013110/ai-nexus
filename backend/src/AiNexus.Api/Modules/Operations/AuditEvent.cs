namespace AiNexus.Modules.Operations;

public static class AuditOutcomes
{
    // Legacy records retain their original result; filters must recognize every successful operation.
    public static readonly string[] Accepted = ["saved", "read", "completed", "success", "granted", "granted_once", "created", "deleted", "soft_deleted", "queued", "running", "cancelled", "revoked", "removed", "assigned", "read-only", "private", "started", "restored"];
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
    public string? Result { get; set; }
    public string? DetailsJson { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
