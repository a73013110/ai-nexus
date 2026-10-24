using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;

namespace AiNexus.Features.Staff.Attendance.Cards;

/// <summary>
/// 一段卡號持有期間 [<see cref="StartsAt"/>, <see cref="EndsAt"/>)。<see cref="UserDisplayName"/> 取自使用者主檔，有帳號時顯示以它為準；
/// <see cref="HolderName"/> 是還沒有帳號時留下的姓名，綁上使用者後仍保留。
/// </summary>
public sealed record CardAssignmentDto(
    Guid Id,
    Guid CardId,
    string CardNumber,
    Guid? UserId,
    string? UserDisplayName,
    string? HolderName,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string? Note,
    DateTimeOffset CreatedAt);

internal static class CardAssignmentQueries
{
    /// <summary>涵蓋 <paramref name="at"/> 這一刻的持有期間；與歸戶 view 同一個半開區間。</summary>
    public static IQueryable<CardAssignment> CoveringAt(this IQueryable<CardAssignment> assignments, DateTimeOffset at)
        => assignments.Where(x => x.StartsAt <= at && (x.EndsAt == null || x.EndsAt > at));

    /// <summary>在 <paramref name="at"/> 還沒結束的持有期間（持有中或尚未開始）。</summary>
    public static IQueryable<CardAssignment> UnendedAt(this IQueryable<CardAssignment> assignments, DateTimeOffset at)
        => assignments.Where(x => x.EndsAt == null || x.EndsAt > at);

    /// <summary>持有期間連同卡號與使用者顯示名稱；使用者可能沒有（只有姓名的持有人），所以是 left join。</summary>
    public static IQueryable<CardAssignmentDto> ToDtos(this IQueryable<CardAssignment> assignments, AppDbContext db)
        => from a in assignments
           join c in db.Set<Card>() on a.CardId equals c.Id
           join u in db.Users on a.UserId equals u.Id into users
           from u in users.DefaultIfEmpty()
           select new CardAssignmentDto(a.Id, a.CardId, c.CardNumber, a.UserId, u.DisplayName, a.HolderName, a.StartsAt, a.EndsAt, a.Note, a.CreatedAt);
}
