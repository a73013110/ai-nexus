namespace AiNexus.Features.Platform.Inference.Runs;

/// <summary>
/// 生成任務狀態與內容資料傳輸物件。
/// </summary>
public sealed record RunDto(
    Guid Id,
    Guid ConversationId,
    Guid UserMessageId,
    Guid AssistantMessageId,
    string ModelId,
    string Status,
    string Content,
    long LastSequence,
    string? ErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    long? InputTokens,
    long? OutputTokens,
    RunTimingDto? Timing = null,
    string? ModelDisplayName = null,
    string? IssueCode = null);
