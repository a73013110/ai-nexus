using AiNexus.Platform.Errors;

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

public sealed record RunDto(Guid Id, Guid ConversationId, Guid UserMessageId, Guid AssistantMessageId, string ModelId, string Status, string Content, long LastSequence, string? ErrorCode, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, long? InputTokens, long? OutputTokens, AiNexus.Features.Inference.RunTimingDto? Timing = null, string? ModelDisplayName = null, string? IssueCode = null);
public sealed record RunEventDto(int Version, long Sequence, Guid RunId, string Type, string Status, string? Delta, string? ErrorCode, string? IssueCode = null);

public static class RunStates
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Failed = "failed";
    public static bool IsActive(string status) => status is Queued or Running;
}

internal static class InferenceErrors
{
    public const string InputTooLongCode = "input_too_long";
    public static readonly Error RunNotFound = Error.NotFound("run_not_found");
    public static readonly Error InvalidModelPolicy = Error.Invalid("invalid_model_policy");
    public static readonly Error IdempotencyKeyRequired = Error.Invalid("idempotency_key_required");
    public static readonly Error IdempotencyConflict = Error.Conflict("idempotency_conflict");
    public static readonly Error GenerationActive = Error.Conflict("generation_active");
    public static readonly Error QueueFull = new(ErrorKind.RateLimited, "queue_full");
    public static readonly Error KnowledgeSelectionChanged = Error.Conflict("knowledge_selection_changed");
    /// <summary>A regeneration reuses the original prompt's attachments.</summary>
    public static readonly Error RegenerateWithAttachments = Error.Invalid("invalid_request");
}
