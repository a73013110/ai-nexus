using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 出勤紀錄人員選單的一個人。<paramref name="UserId"/> 為 null 是只有姓名、還沒綁帳號的持有人，以 <paramref name="DisplayName"/> 查詢（<c>holder</c>）；
/// <paramref name="Holding"/> 是現在仍持有員工卡（離職或換卡空窗的人仍列出，查得到他過去的出勤）。
/// </summary>
public sealed record AttendancePersonOptionDto(Guid? UserId, string DisplayName, string? Account, bool Excluded, bool Holding);

/// <summary>
/// 出勤紀錄的人員選單：範圍看得到、持有過員工卡的人，不限期間，依姓名排序。
/// 與 <see cref="ListAttendanceRecords"/> 用同一份 <see cref="AttendanceRoster"/>，選得到的人就查得到；不列入出勤的人也列出（附旗標），指定他時照常顯示。
/// 一次回整份：名單是公司持卡人（數百人），選單在前端搜尋。
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
        var holdings = await roster.HoldingsAsync(viewer, scope, new() { IncludeExcluded = true }, null, ct);
        var now = clock.GetUtcNow();
        return Result<IReadOnlyList<AttendancePersonOptionDto>>.Ok(
        [
            .. (await roster.PeopleAsync(holdings, ct)).Select(x => new AttendancePersonOptionDto(x.UserId, x.DisplayName, x.Account, x.Excluded,
                x.Holdings.Any(h => h.StartsAt <= now && (h.EndsAt is null || h.EndsAt > now))))
        ]);
    }
}
