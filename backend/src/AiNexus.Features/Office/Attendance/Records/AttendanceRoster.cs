using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Organization;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 名單上的一列，三種之一：
/// <list type="bullet">
/// <item>使用者（<see cref="UserId"/>）：他持有員工卡的期間。</item>
/// <item>只有姓名的持有人（<see cref="CardNumber"/>＋持有期間的姓名）：沒有帳號的資料依附在卡號上，同名同姓在不同卡上是不同列；綁上帳號後併入那位使用者。</item>
/// <item>未歸戶卡號（<see cref="Unattributed"/>）：卡上沒有持有期間涵蓋的刷卡，沒有持有期間，只列有刷卡的日子。</item>
/// </list>
/// </summary>
internal sealed record RosterPerson(Guid? UserId, string? CardNumber, string DisplayName, string? Account, bool Excluded, bool Unattributed,
    IReadOnlyList<RosterHolding> Holdings);

/// <summary>一段員工卡持有期間 [<see cref="StartsAt"/>, <see cref="EndsAt"/>)。</summary>
internal sealed record RosterHolding(Guid Id, DateTimeOffset StartsAt, DateTimeOffset? EndsAt);

/// <summary>名單的來源：持有期間，以及（全公司範圍才有的）未歸戶刷卡。</summary>
internal sealed record RosterSource(IQueryable<CardAssignment> Holdings, IQueryable<AttributedPunch>? Unattributed);

/// <summary>
/// 出勤名單（ADR 0018）：資料範圍看得到的員工卡持有人，全公司範圍另可含未歸戶卡號。出勤紀錄、匯出與人員選單共用同一套範圍與篩選，免得選單列出查不到的人。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>範圍與活動稽核相同：own＝自己，unit／unit-tree＝範圍單位「目前」的成員（組織不存歷史，人調走後由新單位的主管看到）。</item>
/// <item>沒有帳號的資料（只有姓名的持有人、未歸戶卡號）判斷不了本人、單位或主管，只有全公司範圍（all）看得到；指定使用者或單位時也不在其中。</item>
/// <item>「不列入出勤」只是 HR 看全公司時隱藏不重要的人，不是隱私控制：使用者依 <see cref="RosterExclusion"/>、沒有帳號的資料依 <see cref="RosterCardExclusion"/>（卡號），
/// 只在範圍是 all、沒有指定人、也沒有要求含不列入時隱藏。</item>
/// <item>未歸戶卡號預設不列（<c>includeUnattributed</c> 才列），指定卡號時一律列出。</item>
/// </list>
/// </remarks>
internal sealed class AttendanceRoster(AppDbContext db, OrganizationDirectory organization)
{
    /// <summary>
    /// 套用範圍與篩選的名單來源；有 <paramref name="period"/> 時只取與該期間（出勤日，含頭尾，台灣時間整天）有交集的，沒有時不限期間（人員選單）。
    /// </summary>
    public async Task<RosterSource> SourceAsync(Guid viewer, EffectiveDataScope scope, AttendanceRecordsFilter filter,
        (DateOnly Start, DateOnly Last)? period, CancellationToken ct)
    {
        var card = filter.CardNumber;
        var hide = scope.All && !filter.NamesSomeone && filter.IncludeExcluded != true;
        var holdings = from a in db.Set<CardAssignment>()
                       join c in db.Set<Card>() on a.CardId equals c.Id
                       where c.Kind == CardKinds.Employee
                       select new { Assignment = a, c.CardNumber };
        if (period is { } days)
        {
            var (periodStart, periodEnd) = Bounds(days);
            holdings = holdings.Where(x => x.Assignment.StartsAt < periodEnd && (x.Assignment.EndsAt == null || x.Assignment.EndsAt > periodStart));
        }
        if (!scope.All)
        {
            var own = scope.Own;
            var members = organization.MemberIds(scope.UnitIds);
            holdings = holdings.Where(x => x.Assignment.UserId != null && (own && x.Assignment.UserId == viewer || members.Contains(x.Assignment.UserId.Value)));
        }
        if (filter.UserId is { } person) holdings = holdings.Where(x => x.Assignment.UserId == person);
        if (card is not null) holdings = holdings.Where(x => x.Assignment.UserId == null && x.CardNumber == card);
        if (filter.UnitId is { } unit)
        {
            var inUnit = organization.MemberIds(await organization.SubtreeAsync([unit], ct));
            holdings = holdings.Where(x => x.Assignment.UserId != null && inUnit.Contains(x.Assignment.UserId.Value));
        }
        if (hide)
            holdings = holdings.Where(x => x.Assignment.UserId != null
                ? !db.Set<RosterExclusion>().Any(e => e.UserId == x.Assignment.UserId)
                : !db.Set<RosterCardExclusion>().Any(e => e.CardNumber == x.CardNumber));

        IQueryable<AttributedPunch>? unattributed = null;
        if (scope.All && filter.UserId is null && filter.UnitId is null && (card is not null || filter.IncludeUnattributed == true))
        {
            unattributed = db.Set<AttributedPunch>().AsNoTracking().Where(x => x.IsUnattributed);
            if (period is { } range) unattributed = unattributed.Where(x => x.AttendanceDate >= range.Start && x.AttendanceDate <= range.Last);
            if (card is not null) unattributed = unattributed.Where(x => x.CardNumber == card);
            if (hide) unattributed = unattributed.Where(x => !db.Set<RosterCardExclusion>().Any(e => e.CardNumber == x.CardNumber));
        }
        return new(holdings.Select(x => x.Assignment), unattributed);
    }

    /// <summary>
    /// 依列分組並排序（與畫面、匯出的順序相同）：有姓名的列依姓名、帳號，未歸戶卡號排在最後、依卡號；分頁在這份清單上做。
    /// </summary>
    public async Task<IReadOnlyList<RosterPerson>> PeopleAsync(RosterSource source, CancellationToken ct)
    {
        var rows = await source.Holdings
            .Join(db.Set<Card>(), a => a.CardId, c => c.Id, (a, c) => new { a, c.CardNumber })
            .LeftJoin(db.Users, x => x.a.UserId, u => (Guid?)u.Id, (x, u) => new
            {
                x.a.Id, x.a.UserId, x.a.HolderName, x.CardNumber, DisplayName = u == null ? null : u.DisplayName, Account = u == null ? null : u.Account,
                x.a.StartsAt, x.a.EndsAt,
                Excluded = x.a.UserId != null
                    ? db.Set<RosterExclusion>().Any(e => e.UserId == x.a.UserId)
                    : db.Set<RosterCardExclusion>().Any(e => e.CardNumber == x.CardNumber),
            })
            .ToListAsync(ct);
        var named = rows
            .Where(x => x.UserId is not null || x.HolderName is not null)
            .GroupBy(x => (x.UserId, Card: x.UserId is null ? x.CardNumber : null, Name: x.UserId is null ? x.HolderName : null))
            .Select(g =>
            {
                var first = g.First();
                return new RosterPerson(g.Key.UserId, g.Key.Card, first.DisplayName ?? first.HolderName ?? "", first.Account, first.Excluded, false,
                    [.. g.Select(x => new RosterHolding(x.Id, x.StartsAt, x.EndsAt))]);
            })
            .OrderBy(x => x.DisplayName, StringComparer.Ordinal).ThenBy(x => x.Account, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.CardNumber, StringComparer.Ordinal).ThenBy(x => x.UserId);
        if (source.Unattributed is null) return [.. named];

        var cards = await source.Unattributed
            .Select(x => x.CardNumber).Distinct()
            .Select(number => new { Number = number, Excluded = db.Set<RosterCardExclusion>().Any(e => e.CardNumber == number) })
            .ToListAsync(ct);
        return
        [
            .. named,
            .. cards.OrderBy(x => x.Number, StringComparer.Ordinal).Select(x => new RosterPerson(null, x.Number, UnattributedName(x.Number), null, x.Excluded, true, []))
        ];
    }

    /// <summary>未歸戶卡號在名單上的名稱；畫面與匯出共用。</summary>
    public static string UnattributedName(string cardNumber) => $"卡號 {cardNumber}";

    /// <summary>出勤日（含頭尾）換成台灣時間整天的 [開始, 結束)。</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) Bounds((DateOnly Start, DateOnly Last) days)
        => (TaipeiTime.At(days.Start, TimeOnly.MinValue), TaipeiTime.At(days.Last.AddDays(1), TimeOnly.MinValue));
}
