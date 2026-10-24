using AiNexus.Data.App.Attendance;

namespace AiNexus.Features.Office.Attendance.Cards;

/// <summary>
/// 卡號對應頁的一張卡：現在的持有期間（涵蓋目前時間的那一段，沒有人持有時 <see cref="Current"/> 為 null）與刷卡概況。
/// <see cref="UnattributedCount"/> 是沒有持有期間涵蓋的刷卡筆數（不論是否隱藏）；<see cref="FirstUnattributedAt"/> 是補指派時開始時間的預設值，
/// 從第一筆未歸戶刷卡開始持有，之後的刷卡才會全部歸戶。
/// </summary>
public sealed record CardDto(
    Guid Id,
    string CardNumber,
    bool HiddenFromRecords,
    string? Note,
    DateTimeOffset CreatedAt,
    CardAssignmentDto? Current,
    DateTimeOffset? LastPunchedAt,
    int UnattributedCount,
    DateTimeOffset? FirstUnattributedAt,
    DateTimeOffset? LastUnattributedAt)
{
    /// <summary>剛建立、還沒有持有期間與刷卡的卡。</summary>
    public static CardDto New(Card card) => new(card.Id, card.CardNumber, card.HiddenFromRecords, card.Note, card.CreatedAt, null, null, 0, null, null);
}
