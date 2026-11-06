using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Organization;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Time;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 名單上的一列：使用者（<see cref="UserId"/>）他持有卡的期間，或只有姓名的持有人（<see cref="CardNumber"/>＋持有期間的姓名）。
/// 沒有帳號的資料依附在卡號上，同名同姓在不同卡上是不同列；綁上帳號後併入那位使用者。
/// <see cref="Hidden"/> 是這些期間的卡全部在出勤紀錄隱藏（只有指定這個人時才會出現在名單上）。
/// </summary>
internal sealed record RosterPerson(Guid? UserId, string? CardNumber, string DisplayName, string? Account, bool Hidden, IReadOnlyList<RosterHolding> Holdings);

/// <summary>一段持有期間 [<see cref="StartsAt"/>, <see cref="EndsAt"/>)。</summary>
internal sealed record RosterHolding(Guid Id, DateTimeOffset StartsAt, DateTimeOffset? EndsAt);

/// <summary>
/// 出勤名單（ADR 0018）：資料範圍看得到的持卡人。出勤紀錄、匯出與人員選單共用同一套範圍與篩選，免得選單列出查不到的人。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>範圍與活動稽核相同：own＝自己，unit／unit-tree＝範圍單位「目前」的成員（組織不存歷史，人調走後由新單位的主管看到）。</item>
/// <item>只有姓名的持有人判斷不了本人、單位或主管，只有全公司範圍（all）看得到；指定使用者或單位時也不在其中。</item>
/// <item>在出勤紀錄隱藏（<see cref="Card.HiddenFromRecords"/>）只是 HR 看全公司時略過不重要的卡，不是隱私控制：
/// 只在範圍是 all、也沒有指定人時略過那些卡的持有期間，本人與主管照常看得到。</item>
/// <item>沒有持有期間涵蓋的刷卡不屬於任何人，不在名單；在卡號對應頁以「待指派」處理。</item>
/// </list>
/// </remarks>
[Scoped]
internal sealed class AttendanceRoster(AppDbContext db, OrganizationDirectory organization)
{
    /// <summary>
    /// 名單怎麼組成的條文（給助理的〈規則〉）：與這個類別和 <see cref="ListAttendanceRecords"/> 的條件一一對應，改條件時一起改。
    /// 「為什麼某人還在、為什麼不見」由這些條文加上那個人的持卡設定說明，不逐案寫回答規則。
    /// </summary>
    public static IReadOnlyList<(string Title, string Text)> Rules { get; } =
    [
        ("誰會列出", "查詢區間內持有出勤卡的人才列出。持有期間沒有結束時間就是持有中，之後每個上班日都會判定，沒有刷卡就是未刷卡。"),
        ("帳號停用", "帳號停用（離職）不會自動結束持有期間：要在「卡號對應」把結束時間設在離職生效日，否則離職後仍會每天列為未刷卡。"),
        ("持卡以外的日子", "只列持有卡的日子：開始持有之前、換卡的空窗與持有結束之後都不列，也不判定。今天之後才開始的持有期間，現在不會列出。"),
        ("隱藏的卡", "卡號設為「在出勤紀錄隱藏」時，資料範圍是全部的人看整份名單不會列出它；指定這個人查詢時、本人與依單位看的主管仍看得到。這是 HR 略過不重要的卡用的，不是權限。"),
        ("沒有帳號的持有人", "持有期間還沒綁帳號時只有姓名，只有資料範圍是全部的人看得到，表單也對不到。帳號建立後在「卡號對應」綁上，先前的刷卡就會歸給他。"),
        ("依單位的範圍", "所屬單位或所管單位的範圍看的是單位目前的成員：調單位後由新單位看到，原單位看不到他過去的紀錄。"),
        ("沒有人持有的刷卡", "刷卡時間沒有任何持有期間涵蓋時不屬於任何人，不在名單上，要在「卡號對應」的待指派處理。"),
    ];

    /// <summary>
    /// 套用範圍與篩選的持有期間；有 <paramref name="period"/> 時只取與該期間（出勤日，含頭尾，台灣時間整天）有交集的，沒有時不限期間（人員選單）。
    /// <paramref name="includeHidden"/> 時連隱藏的卡也列（人員選單要讓 HR 指定得到被隱藏的人）。
    /// </summary>
    public async Task<IQueryable<CardAssignment>> HoldingsAsync(Guid viewer, EffectiveDataScope scope, AttendanceRecordsFilter filter,
        (DateOnly Start, DateOnly Last)? period, bool includeHidden, CancellationToken ct)
    {
        var holdings = from a in db.CardAssignments
                       join c in db.Cards on a.CardId equals c.Id
                       select new { Assignment = a, c.CardNumber, c.HiddenFromRecords };
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
        if (filter.CardNumber is { } card) holdings = holdings.Where(x => x.Assignment.UserId == null && x.CardNumber == card);
        if (filter.UnitId is { } unit)
        {
            var inUnit = organization.MemberIds(await organization.SubtreeAsync([unit], ct));
            holdings = holdings.Where(x => x.Assignment.UserId != null && inUnit.Contains(x.Assignment.UserId.Value));
        }
        if (!includeHidden && scope.All && !filter.NamesSomeone) holdings = holdings.Where(x => !x.HiddenFromRecords);
        return holdings.Select(x => x.Assignment);
    }

    /// <summary>依人分組並依使用者順序（<see cref="UserOrder"/>：管理者設定的排序，未排序的依姓名、帳號）排列，與畫面、匯出的順序相同；分頁在這份清單上做。</summary>
    public async Task<IReadOnlyList<RosterPerson>> PeopleAsync(IQueryable<CardAssignment> holdings, CancellationToken ct)
    {
        var rows = await holdings
            .Join(db.Cards, a => a.CardId, c => c.Id, (a, c) => new { a, c.CardNumber, c.HiddenFromRecords })
            .LeftJoin(db.Users, x => x.a.UserId, u => (Guid?)u.Id, (x, u) => new
            {
                x.a.Id, x.a.UserId, x.a.HolderName, x.CardNumber, x.HiddenFromRecords, DisplayName = u == null ? null : u.DisplayName, Account = u == null ? null : u.AdAccount ?? u.LocalAccount,
                SortOrder = u == null ? null : u.SortOrder, x.a.StartsAt, x.a.EndsAt,
            })
            .ToListAsync(ct);
        return
        [
            .. rows
                .Where(x => x.UserId is not null || x.HolderName is not null)
                .GroupBy(x => (x.UserId, Card: x.UserId is null ? x.CardNumber : null, Name: x.UserId is null ? x.HolderName : null))
                .Select(g =>
                {
                    var first = g.First();
                    return (first.SortOrder, Person: new RosterPerson(g.Key.UserId, g.Key.Card, first.DisplayName ?? first.HolderName ?? "", first.Account, g.All(x => x.HiddenFromRecords),
                        [.. g.Select(x => new RosterHolding(x.Id, x.StartsAt, x.EndsAt))]));
                })
                .InUserOrder(x => x.SortOrder, x => x.Person.DisplayName, x => x.Person.Account)
                .ThenBy(x => x.Person.CardNumber, StringComparer.Ordinal).ThenBy(x => x.Person.UserId)
                .Select(x => x.Person)
        ];
    }

    /// <summary>出勤日（含頭尾）換成台灣時間整天的 [開始, 結束)。</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) Bounds((DateOnly Start, DateOnly Last) days)
        => (TaipeiTime.At(days.Start, TimeOnly.MinValue), TaipeiTime.At(days.Last.AddDays(1), TimeOnly.MinValue));
}
