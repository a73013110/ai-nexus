using AiNexus.Data.App.Inference;
using AiNexus.Features.Inference.Models;
using AiNexus.Features.Inference.Runs;
namespace AiNexus.Features.Inference;

/// <summary>
/// 推論領域模型與對外傳輸物件（DTO）映射器。
/// </summary>
public static class InferenceMappings
{
    public static RunDto ToDto(this GenerationRun x) => new(
        x.Id,
        x.ConversationId,
        x.UserMessageId,
        x.AssistantMessageId,
        x.ModelId,
        x.Status,
        x.Content,
        x.LastSequence,
        x.ErrorCode,
        x.CreatedAt,
        x.StartedAt,
        x.FinishedAt,
        x.InputTokens,
        x.OutputTokens,
        RunTiming.Describe(x),
        IssueCode: x.IssueCode);

    public static RunEventDto ToDto(this RunEvent x) => new(
        1,
        x.Sequence,
        x.RunId,
        x.Type,
        x.Status,
        x.Delta,
        x.ErrorCode,
        x.IssueCode);

    public static ModelDto ToDto(this ModelDefinition x) => new(
        x.Id,
        x.DisplayName,
        x.ContextTokens,
        x.MaxOutputTokens,
        x.SupportsStreaming,
        x.SupportsUsage,
        x.ReasoningEfforts,
        x.DefaultReasoningEffort,
        x.SupportsImages,
        x.Provider);
}
