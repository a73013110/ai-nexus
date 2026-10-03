namespace AiNexus.Modules.Operations;

public sealed class AuditEvent
{
    public long Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Action { get; set; } = "";
    public Guid? ResourceId { get; set; }
    public string? Result { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
