namespace AiNexus.Features.Inference;

public static class InferenceMappings
{
    public static RunDto ToDto(this GenerationRun x) => new(x.Id, x.ConversationId, x.UserMessageId, x.AssistantMessageId, x.ModelId, x.Status, x.Content, x.LastSequence, x.ErrorCode, x.CreatedAt, x.StartedAt, x.FinishedAt, x.InputTokens, x.OutputTokens, RunTiming.Describe(x), IssueCode: x.IssueCode);
    public static RunEventDto ToDto(this RunEvent x) => new(1, x.Sequence, x.RunId, x.Type, x.Status, x.Delta, x.ErrorCode, x.IssueCode);
    public static ModelDto ToDto(this ModelProfile x) => new(x.Id, x.DisplayName, x.ContextTokens, x.MaxOutputTokens, x.SupportsStreaming, x.SupportsUsage, x.ReasoningEfforts, x.DefaultReasoningEffort, x.SupportsImages, x.Provider);
}
