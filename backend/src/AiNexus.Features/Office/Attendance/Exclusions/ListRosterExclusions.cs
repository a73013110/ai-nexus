using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Office.Attendance.Records;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Exclusions;

/// <summary>
/// 不列入出勤名單的一列：使用者（<paramref name="UserId"/>），或一張卡上沒有帳號的資料（<paramref name="CardNumber"/>）。
/// 卡號列的 <paramref name="DisplayName"/> 是最近一位只有姓名的持有人，卡上從沒有姓名持有人時是「卡號 …」；<paramref name="Account"/> 只有使用者有。
/// <paramref name="CreatedAt"/> 是設定時間（修改備註不變）。
/// </summary>
public sealed record RosterExclusionDto(Guid? UserId, string? CardNumber, string DisplayName, string? Account, string? Note, DateTimeOffset CreatedAt);

/// <summary>
/// 不列入出勤的名單（ADR 0018）：使用者與卡號依名稱排序，沒有姓名持有人的卡號排在最後。它只是 HR 看全公司出勤時隱藏不重要的人，不是權限，
/// 所以和卡號對應同一個頁面授權（<c>attendance.cards</c>）；名單是 HR 手動維護的少數人，不分頁。
/// </summary>
internal sealed class ListRosterExclusions(AppDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (ListRosterExclusions handler, CancellationToken ct) => handler.HandleAsync(ct).ToHttpResultAsync())
        .WithName("ListAttendanceRosterExclusions");

    public async Task<Result<IReadOnlyList<RosterExclusionDto>>> HandleAsync(CancellationToken ct)
    {
        var users = await (from x in db.Set<RosterExclusion>().AsNoTracking()
                           join u in db.Users on x.UserId equals u.Id
                           select new RosterExclusionDto(x.UserId, null, u.DisplayName, u.Account, x.Note, x.CreatedAt))
            .ToListAsync(ct);
        var cards = await db.Set<RosterCardExclusion>().AsNoTracking()
            .Select(x => new
            {
                x.CardNumber, x.Note, x.CreatedAt,
                Holder = (from a in db.Set<CardAssignment>()
                          join c in db.Set<Card>() on a.CardId equals c.Id
                          where c.CardNumber == x.CardNumber && a.UserId == null && a.HolderName != null
                          orderby a.StartsAt descending
                          select a.HolderName).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return Result<IReadOnlyList<RosterExclusionDto>>.Ok(
        [
            .. users.Concat(cards.Where(x => x.Holder is not null).Select(x => new RosterExclusionDto(null, x.CardNumber, x.Holder!, null, x.Note, x.CreatedAt)))
                .OrderBy(x => x.DisplayName, StringComparer.Ordinal).ThenBy(x => x.Account ?? x.CardNumber, StringComparer.OrdinalIgnoreCase),
            .. cards.Where(x => x.Holder is null).OrderBy(x => x.CardNumber, StringComparer.Ordinal)
                .Select(x => new RosterExclusionDto(null, x.CardNumber, AttendanceRoster.UnattributedName(x.CardNumber), null, x.Note, x.CreatedAt))
        ]);
    }
}
