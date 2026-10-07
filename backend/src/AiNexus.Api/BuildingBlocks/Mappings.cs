using AiNexus.Modules.Conversations;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Inference;

namespace AiNexus.BuildingBlocks;

public static class Mappings
{
    public static PreferencesDto ToDto(this UserPreferences x) => new(x.Theme, x.ReducedMotion, x.DefaultModelId);
    public static ConversationDto ToDto(this Conversation x) => new(x.Id, x.Title, x.ActiveLeafId, x.CreatedAt, x.UpdatedAt, x.IsFavorite, x.IsArchived, x.SystemInstruction, x.Labels.Select(l => l.Name).Order().ToArray(), x.ProjectId);
    public static MessageDto ToDto(this Message x) => new(x.Id, x.ParentId, x.Role, x.Content, x.Status, x.CreatedAt, x.RunId, x.ModelId, [], x.ErrorCode, IssueCode: x.IssueCode);
    public static RunDto ToDto(this GenerationRun x) => new(x.Id, x.ConversationId, x.UserMessageId, x.AssistantMessageId, x.ModelId, x.Status, x.Content, x.LastSequence, x.ErrorCode, x.CreatedAt, x.StartedAt, x.FinishedAt, x.InputTokens, x.OutputTokens, RunTiming.Describe(x), IssueCode: x.IssueCode);
    public static RunEventDto ToDto(this RunEvent x) => new(1, x.Sequence, x.RunId, x.Type, x.Status, x.Delta, x.ErrorCode, x.IssueCode);
    public static ModelDto ToDto(this ModelProfile x) => new(x.Id, x.DisplayName, x.ContextTokens, x.MaxOutputTokens, x.SupportsStreaming, x.SupportsUsage, x.ReasoningEfforts, x.DefaultReasoningEffort, x.SupportsImages, x.Provider);
}
