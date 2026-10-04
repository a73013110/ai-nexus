namespace AiNexus.BuildingBlocks;

public sealed record PreferencesDto(string Theme, bool ReducedMotion, string? DefaultModelId);
public sealed record MeDto(Guid Id, string Account, string DisplayName, PreferencesDto Preferences, string CsrfToken, Guid? ActiveRunId, AiNexus.Modules.AccessControl.AccessDto Access);
public sealed record ModelDto(string Id, string DisplayName, int ContextTokens, int MaxOutputTokens, bool SupportsStreaming, bool SupportsUsage, IReadOnlyList<string> ReasoningEfforts, string DefaultReasoningEffort, bool SupportsImages = false);
public sealed record ModelPolicyDto(bool AllowModelSelection, bool ShowModelNames, string? DefaultModelId, int MaxInputCharacters = 12000);
public sealed record ModelsDto(IReadOnlyList<ModelDto> Models, bool ProviderAvailable, string? Notice, ModelPolicyDto Policy);
public sealed record ConversationDto(Guid Id, string Title, Guid? ActiveLeafId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool IsFavorite = false, bool IsArchived = false, string SystemInstruction = "", IReadOnlyList<string>? Labels = null, Guid? ProjectId = null);
public sealed record MessageDto(Guid Id, Guid? ParentId, string Role, string Content, string Status, DateTimeOffset CreatedAt, Guid? RunId, string? ModelId, IReadOnlyList<AiNexus.Modules.Attachments.AttachmentDto>? Attachments = null, string? ErrorCode = null, IReadOnlyList<AiNexus.Modules.Knowledge.CitationDto>? Sources = null);
public sealed record ConversationDetailDto(ConversationDto Conversation, IReadOnlyList<MessageDto> Messages, RunDto? ActiveRun);
public sealed record RunDto(Guid Id, Guid ConversationId, Guid UserMessageId, Guid AssistantMessageId, string ModelId, string Status, string Content, long LastSequence, string? ErrorCode, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, long? InputTokens, long? OutputTokens);
public sealed record RunEventDto(int Version, long Sequence, Guid RunId, string Type, string Status, string? Delta, string? ErrorCode);
public sealed record CreateConversationRequest(string? Title = null);
public sealed record RenameConversationRequest(string Title);
public sealed record SelectBranchRequest(Guid LeafId);
public sealed record CreateRunRequest(Guid ConversationId, string? ModelId, string? Prompt, Guid? ParentMessageId, Guid? RegenerateUserMessageId, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? ReasoningEffort = null, IReadOnlyList<Guid>? AttachmentIds = null);
public sealed record ContextPreviewRequest(Guid? ConversationId, Guid? ParentMessageId, string? Prompt, string? ModelId, IReadOnlyList<Guid>? AttachmentIds = null);
public sealed record ContextUsageDto(int EstimatedInputTokens, int ContextTokens, int ReservedOutputTokens, int DroppedMessages, bool BudgetExceeded, bool IsEstimate = true, int ReservedKnowledgeTokens = 0);
public sealed record StatusDto(string Storage, string Authentication, int QueueDepth, bool Generating);

public sealed class ApiException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public static class RunStates
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Failed = "failed";
    public static bool IsActive(string status) => status is Queued or Running;
}
