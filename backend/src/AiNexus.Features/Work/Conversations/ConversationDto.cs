using AiNexus.Data.App.Conversations;

namespace AiNexus.Features.Work.Conversations;

/// <summary>對外的對話資料；標籤依名稱排序。</summary>
public sealed record ConversationDto(
    Guid Id,
    string Title,
    Guid? ActiveLeafId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsFavorite = false,
    bool IsArchived = false,
    string SystemInstruction = "",
    IReadOnlyList<string>? Labels = null,
    Guid? ProjectId = null);

public static class ConversationMappings
{
    public static ConversationDto ToDto(this Conversation x) => new(
        x.Id,
        x.Title,
        x.ActiveLeafId,
        x.CreatedAt,
        x.UpdatedAt,
        x.IsFavorite,
        x.IsArchived,
        x.SystemInstruction,
        x.Labels.Select(l => l.Name).Order().ToArray(),
        x.ProjectId);
}
