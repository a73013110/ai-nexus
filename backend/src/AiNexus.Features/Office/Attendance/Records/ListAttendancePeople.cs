using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 出勤紀錄人員選單的一個選項：使用者，或一張卡上沒有帳號的資料（<paramref name="UserId"/> 為 null，以 <paramref name="CardNumber"/> 查詢 <c>card</c>）。
/// 卡號選項的 <paramref name="DisplayName"/> 是最近一位只有姓名的持有人；<paramref name="Unattributed"/> 為 true 是卡上從沒有姓名持有人、只有未歸戶刷卡。
/// <paramref name="Holding"/> 是現在仍持有員工卡（離職或換卡空窗的人仍列出，查得到他過去的出勤）。
/// </summary>
public sealed record AttendancePersonOptionDto(Guid? UserId, string? CardNumber, string DisplayName, string? Account, bool Excluded, bool Holding, bool Unattributed);

/// <summary>
/// 出勤紀錄的人員選單：範圍看得到、持有過員工卡的人，不限期間，依姓名排序；全公司範圍另列有未歸戶刷卡的卡號（排在最後）。
/// 與 <see cref="ListAttendanceRecords"/> 用同一份 <see cref="AttendanceRoster"/>，選得到的就查得到；不列入出勤的也列出（附旗標），指定時照常顯示。
/// 沒有帳號的資料以卡號查詢，同一張卡上的姓名持有人與未歸戶刷卡合成一個選項。一次回整份：名單是公司持卡人（數百人），選單在前端搜尋。
/// </summary>
internal sealed class ListAttendancePeople(IDataScope scopes, AttendanceRoster roster, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/people", (ICurrentUser user, ListAttendancePeople handler, CancellationToken ct) => handler.HandleAsync(user.Id, ct).ToHttpResultAsync())
        .WithName("ListAttendancePeople");

    public async Task<Result<IReadOnlyList<AttendancePersonOptionDto>>> HandleAsync(Guid viewer, CancellationToken ct)
    {
        var scope = await scopes.ForAsync(viewer, FeatureCatalog.AttendanceRecords, ct);
        if (scope.IsEmpty) return Result<IReadOnlyList<AttendancePersonOptionDto>>.Ok([]);
        var source = await roster.SourceAsync(viewer, scope, new() { IncludeExcluded = true, IncludeUnattributed = true }, null, ct);
        var now = clock.GetUtcNow();
        bool Holding(RosterPerson person) => person.Holdings.Any(h => h.StartsAt <= now && (h.EndsAt is null || h.EndsAt > now));
        var people = await roster.PeopleAsync(source, ct);
        // 卡號選項：同一張卡的姓名持有人（可能先後不同人）與未歸戶刷卡合成一個，名稱取最近的姓名持有人。
        var cards = people.Where(x => x.UserId is null)
            .GroupBy(x => x.CardNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        List<AttendancePersonOptionDto> options = [];
        foreach (var person in people)
        {
            if (person.UserId is not null)
            {
                options.Add(new(person.UserId, null, person.DisplayName, person.Account, person.Excluded, Holding(person), false));
                continue;
            }
            if (!cards.Remove(person.CardNumber!, out var rows)) continue;
            var named = rows.Where(x => !x.Unattributed).OrderByDescending(x => x.Holdings.Max(h => h.StartsAt)).FirstOrDefault();
            options.Add(new(null, person.CardNumber, named?.DisplayName ?? person.DisplayName, null, person.Excluded, rows.Any(Holding), named is null));
        }
        return Result<IReadOnlyList<AttendancePersonOptionDto>>.Ok(options);
    }
}
