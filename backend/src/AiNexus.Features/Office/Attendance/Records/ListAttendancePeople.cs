using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 出勤紀錄人員選單的一個選項：使用者，或一張卡上只有姓名的持有人（<paramref name="UserId"/> 為 null，以 <paramref name="CardNumber"/> 查詢 <c>card</c>）。
/// 卡號選項的 <paramref name="DisplayName"/> 是最近一位只有姓名的持有人。<paramref name="Hidden"/> 是他持有的卡全部在出勤紀錄隱藏，
/// <paramref name="Holding"/> 是現在仍持有卡（離職或換卡空窗的人仍列出，查得到他過去的出勤）。
/// </summary>
public sealed record AttendancePersonOptionDto(Guid? UserId, string? CardNumber, string DisplayName, string? Account, bool Hidden, bool Holding);

/// <summary>
/// 出勤紀錄的人員選單：範圍看得到、持有過卡的人，不限期間，順序與出勤紀錄相同（使用者順序）。與 <see cref="ListAttendanceRecords"/> 用同一份 <see cref="AttendanceRoster"/>，
/// 選得到的就查得到；隱藏的卡的持有人也列出（附旗標），指定時照常顯示。同一張卡先後的姓名持有人合成一個選項。
/// 一次回整份：名單是公司持卡人（數百人），選單在前端搜尋。
/// <c>unitId</c> 與查詢的單位篩選同一套（該單位及下層的目前成員，沒有帳號的卡號持有人不列），畫面選了單位後人員選單只列那個單位的人。
/// </summary>
internal sealed class ListAttendancePeople(IDataScope scopes, AttendanceRoster roster, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/people", (Guid? unitId, ICurrentUser user, ListAttendancePeople handler, CancellationToken ct) => handler.HandleAsync(user.Id, unitId, ct).ToHttpResultAsync())
        .WithName("ListAttendancePeople");

    public async Task<Result<IReadOnlyList<AttendancePersonOptionDto>>> HandleAsync(Guid viewer, Guid? unitId, CancellationToken ct)
    {
        var scope = await scopes.ForAsync(viewer, FeatureCatalog.AttendanceRecords, ct);
        if (scope.IsEmpty) return Result<IReadOnlyList<AttendancePersonOptionDto>>.Ok([]);
        var holdings = await roster.HoldingsAsync(viewer, scope, new() { UnitId = unitId }, null, includeHidden: true, ct);
        var now = clock.GetUtcNow();
        bool Holding(RosterPerson person) => person.Holdings.Any(h => h.StartsAt <= now && (h.EndsAt is null || h.EndsAt > now));
        var people = await roster.PeopleAsync(holdings, ct);
        // 卡號選項：同一張卡先後的姓名持有人合成一個，名稱取最近的那位。
        var cards = people.Where(x => x.UserId is null)
            .GroupBy(x => x.CardNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        List<AttendancePersonOptionDto> options = [];
        foreach (var person in people)
        {
            if (person.UserId is not null)
            {
                options.Add(new(person.UserId, null, person.DisplayName, person.Account, person.Hidden, Holding(person)));
                continue;
            }
            if (!cards.Remove(person.CardNumber!, out var rows)) continue;
            var latest = rows.MaxBy(x => x.Holdings.Max(h => h.StartsAt))!;
            options.Add(new(null, person.CardNumber, latest.DisplayName, null, rows.All(x => x.Hidden), rows.Any(Holding)));
        }
        return Result<IReadOnlyList<AttendancePersonOptionDto>>.Ok(options);
    }
}
