namespace AiNexus.Features.Inference;

public sealed record ModelDto(string Id, string DisplayName, int ContextTokens, int MaxOutputTokens, bool SupportsStreaming, bool SupportsUsage, IReadOnlyList<string> ReasoningEfforts, string DefaultReasoningEffort, bool SupportsImages = false, string? Provider = null);
public sealed record ModelPolicyDto(bool AllowModelSelection, bool ShowModelNames, string? DefaultModelId, int MaxInputCharacters = 12000);
public sealed record ModelsDto(IReadOnlyList<ModelDto> Models, bool ProviderAvailable, string? Notice, ModelPolicyDto Policy, IReadOnlyList<AiNexus.Features.Inference.ProviderStatusDto>? Providers = null);
public sealed record RunDto(Guid Id, Guid ConversationId, Guid UserMessageId, Guid AssistantMessageId, string ModelId, string Status, string Content, long LastSequence, string? ErrorCode, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, long? InputTokens, long? OutputTokens, AiNexus.Features.Inference.RunTimingDto? Timing = null, string? ModelDisplayName = null, string? IssueCode = null);
public sealed record RunEventDto(int Version, long Sequence, Guid RunId, string Type, string Status, string? Delta, string? ErrorCode, string? IssueCode = null);
public sealed record CreateRunRequest(Guid ConversationId, string? ModelId, string? Prompt, Guid? ParentMessageId, Guid? RegenerateUserMessageId, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? ReasoningEffort = null, IReadOnlyList<Guid>? AttachmentIds = null, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool WebSearch = false);
public sealed record ContextPreviewRequest(Guid? ConversationId, Guid? ParentMessageId, string? Prompt, string? ModelId, IReadOnlyList<Guid>? AttachmentIds = null, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool WebSearch = false);
public sealed record ContextUsageDto(int EstimatedInputTokens, int ContextTokens, int ReservedOutputTokens, int DroppedMessages, bool BudgetExceeded, bool IsEstimate = true, int ReservedKnowledgeTokens = 0, int ReservedWebSearchTokens = 0);

public static class RunStates
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Failed = "failed";
    public static bool IsActive(string status) => status is Queued or Running;
}
