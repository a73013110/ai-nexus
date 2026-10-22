using AiNexus.Data.App.Attendance;

namespace AiNexus.Features.Staff.Attendance.Cards;

/// <summary>卡片與它現在的持有期間（涵蓋目前時間的那一段）；沒有人持有時 <see cref="Current"/> 為 null。</summary>
public sealed record CardDto(Guid Id, string CardNumber, string Kind, string? Note, DateTimeOffset CreatedAt, CardAssignmentDto? Current)
{
    public static CardDto From(Card card, CardAssignmentDto? current) => new(card.Id, card.CardNumber, card.Kind, card.Note, card.CreatedAt, current);
}
