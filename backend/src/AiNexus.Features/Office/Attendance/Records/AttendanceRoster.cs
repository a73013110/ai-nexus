using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Organization;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 名單上的一個人：使用者，或只有姓名、還沒綁帳號的持有人（<see cref="UserId"/> 為 null，<see cref="DisplayName"/> 是持有期間的姓名）。
/// 同名的姓名持有人視為同一人；綁上帳號後他的持有期間改歸那位使用者，自然併入。
/// </summary>
internal sealed record RosterPerson(Guid? UserId, string DisplayName, string? Account, bool Excluded, IReadOnlyList<RosterHolding> Holdings);

/// <summary>一段員工卡持有期間 [<see cref="StartsAt"/>, <see cref="EndsAt"/>)。</summary>
internal sealed record RosterHolding(Guid Id, DateTimeOffset StartsAt, DateTimeOffset? EndsAt);

/// <summary>
/// 出勤名單（ADR 0018）：資料範圍看得到的員工卡持有人。出勤紀錄、匯出與人員選單共用同一套範圍與篩選，免得選單列出查不到的人。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>範圍與活動稽核相同：own＝自己，unit／unit-tree＝範圍單位「目前」的成員（組織不存歷史，人調走後由新單位的主管看到）。</item>
/// <item>只有姓名的持有人沒有使用者，判斷不了本人、單位或主管，只有全公司範圍（all）看得到；指定使用者或單位時也不在其中。</item>
/// <item>「不列入出勤」只是 HR 看全公司時隱藏不重要的人，不是隱私控制：只在範圍是 all、沒有指定人、也沒有要求含不列入時隱藏。</item>
/// </list>
/// </remarks>
internal sealed class AttendanceRoster(AppDbContext db, OrganizationDirectory organization)
{
    /// <summary>
    /// 套用範圍與篩選的員工卡持有期間；有 <paramref name="period"/> 時只取與該期間（出勤日，含頭尾，台灣時間整天）有交集的，沒有時不限期間（人員選單）。
    /// </summary>
    public async Task<IQueryable<CardAssignment>> HoldingsAsync(Guid viewer, EffectiveDataScope scope, AttendanceRecordsFilter filter,
        (DateOnly Start, DateOnly Last)? period, CancellationToken ct)
    {
        var holdings = from a in db.Set<CardAssignment>()
                       join c in db.Set<Card>() on a.CardId equals c.Id
                       where c.Kind == CardKinds.Employee
                       select a;
        if (period is { } days)
        {
            var (periodStart, periodEnd) = (TaipeiTime.At(days.Start, TimeOnly.MinValue), TaipeiTime.At(days.Last.AddDays(1), TimeOnly.MinValue));
            holdings = holdings.Where(a => a.StartsAt < periodEnd && (a.EndsAt == null || a.EndsAt > periodStart));
        }
        if (!scope.All)
        {
            var own = scope.Own;
            var members = organization.MemberIds(scope.UnitIds);
            holdings = holdings.Where(a => a.UserId != null && (own && a.UserId == viewer || members.Contains(a.UserId.Value)));
        }
        var holder = filter.HolderName;
        if (filter.UserId is { } person) holdings = holdings.Where(a => a.UserId == person);
        if (holder is not null) holdings = holdings.Where(a => a.UserId == null && a.HolderName == holder);
        if (filter.UnitId is { } unit)
        {
            var inUnit = organization.MemberIds(await organization.SubtreeAsync([unit], ct));
            holdings = holdings.Where(a => a.UserId != null && inUnit.Contains(a.UserId.Value));
        }
        if (scope.All && filter.UserId is null && holder is null && filter.IncludeExcluded != true)
            holdings = holdings.Where(a => !db.Set<RosterExclusion>().Any(x => x.UserId == a.UserId));
        return holdings;
    }

    /// <summary>依人分組，依姓名、帳號排序（與畫面、匯出的順序相同）；分頁在這份清單上做。</summary>
    public async Task<IReadOnlyList<RosterPerson>> PeopleAsync(IQueryable<CardAssignment> holdings, CancellationToken ct)
    {
        var rows = await holdings
            .LeftJoin(db.Users, a => a.UserId, u => (Guid?)u.Id, (a, u) => new
            {
                a.Id, a.UserId, a.HolderName, DisplayName = u == null ? null : u.DisplayName, Account = u == null ? null : u.Account, a.StartsAt, a.EndsAt,
                Excluded = a.UserId != null && db.Set<RosterExclusion>().Any(x => x.UserId == a.UserId),
            })
            .ToListAsync(ct);
        return
        [
            .. rows.Where(x => x.UserId is not null || x.HolderName is not null)
                .GroupBy(x => (x.UserId, Name: x.UserId is null ? x.HolderName : null))
                .Select(g =>
                {
                    var first = g.First();
                    return new RosterPerson(g.Key.UserId, first.DisplayName ?? first.HolderName ?? "", first.Account, first.Excluded,
                        [.. g.Select(x => new RosterHolding(x.Id, x.StartsAt, x.EndsAt))]);
                })
                .OrderBy(x => x.DisplayName, StringComparer.Ordinal).ThenBy(x => x.Account, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.UserId)
        ];
    }
}
