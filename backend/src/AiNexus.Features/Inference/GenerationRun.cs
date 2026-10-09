namespace AiNexus.Features.Inference;

/// <summary>One chat answer being generated. Its EF mapping lives in <c>NexusDbContext</c>.</summary>
public sealed class GenerationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string? TraceId { get; set; }
    public string? ParentSpanId { get; set; }
    public Guid OperationId { get; set; }
    // Unique filtered index enforces one active generation per owner, even across requests.
    public Guid? ActiveOwnerId { get; set; }
    public Guid? ExecutorId { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public Guid ConversationId { get; set; }
    public Guid UserMessageId { get; set; }
    public Guid AssistantMessageId { get; set; }
    public string ModelId { get; set; } = "";
    public string Provider { get; set; } = "google";
    public string ProviderModelId { get; set; } = "";
    public string ParametersJson { get; set; } = "{}";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string Status { get; set; } = RunStates.Queued;
    public string Content { get; set; } = "";
    public long LastSequence { get; set; }
    public string? IssueCode { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long ReservedTokens { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? DurationMilliseconds { get; set; }
    public long? GenerationMilliseconds { get; set; }
}
