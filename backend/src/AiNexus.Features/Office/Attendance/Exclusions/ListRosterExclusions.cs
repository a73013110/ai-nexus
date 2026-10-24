using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Exclusions;

/// <summary>標為不列入出勤的一位使用者；<paramref name="CreatedAt"/> 是設定時間（修改備註不變）。</summary>
public sealed record RosterExclusionDto(Guid UserId, string DisplayName, string Account, string? Note, DateTimeOffset CreatedAt);

/// <summary>
/// 不列入出勤的名單（ADR 0018），依姓名排序。它只是 HR 看全公司出勤時隱藏不重要的人，不是權限，所以和卡號對應同一個頁面授權（<c>attendance.cards</c>）；
/// 名單是 HR 手動維護的少數人，不分頁。
/// </summary>
internal sealed class ListRosterExclusions(AppDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (ListRosterExclusions handler, CancellationToken ct) => handler.HandleAsync(ct).ToHttpResultAsync())
        .WithName("ListAttendanceRosterExclusions");

    public async Task<Result<IReadOnlyList<RosterExclusionDto>>> HandleAsync(CancellationToken ct)
    {
        var rows = await (from x in db.Set<RosterExclusion>().AsNoTracking()
                          join u in db.Users on x.UserId equals u.Id
                          select new RosterExclusionDto(x.UserId, u.DisplayName, u.Account, x.Note, x.CreatedAt))
            .ToListAsync(ct);
        return Result<IReadOnlyList<RosterExclusionDto>>.Ok(
            [.. rows.OrderBy(x => x.DisplayName, StringComparer.Ordinal).ThenBy(x => x.Account, StringComparer.OrdinalIgnoreCase)]);
    }
}
