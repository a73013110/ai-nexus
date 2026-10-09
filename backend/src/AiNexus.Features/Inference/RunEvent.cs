namespace AiNexus.Features.Inference;

public sealed class RunEvent
{
    public Guid RunId { get; set; }
    public long Sequence { get; set; }
    public string Type { get; set; } = "status";
    public string Status { get; set; } = RunStates.Queued;
    public string? Delta { get; set; }
    public string? IssueCode { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
