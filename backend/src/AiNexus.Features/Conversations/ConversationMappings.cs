namespace AiNexus.Features.Conversations;

public static class ConversationMappings
{
    public static ConversationDto ToDto(this Conversation x) => new(x.Id, x.Title, x.ActiveLeafId, x.CreatedAt, x.UpdatedAt, x.IsFavorite, x.IsArchived, x.SystemInstruction, x.Labels.Select(l => l.Name).Order().ToArray(), x.ProjectId);
    public static MessageDto ToDto(this Message x) => new(x.Id, x.ParentId, x.Role, x.Content, x.Status, x.CreatedAt, x.RunId, x.ModelId, [], x.ErrorCode, IssueCode: x.IssueCode);
}
