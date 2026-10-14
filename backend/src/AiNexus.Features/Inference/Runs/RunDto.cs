namespace AiNexus.Features.Inference.Runs;

public sealed record RunDto(Guid Id, Guid ConversationId, Guid UserMessageId, Guid AssistantMessageId, string ModelId, string Status, string Content, long LastSequence, string? ErrorCode, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, long? InputTokens, long? OutputTokens, RunTimingDto? Timing = null, string? ModelDisplayName = null, string? IssueCode = null);
