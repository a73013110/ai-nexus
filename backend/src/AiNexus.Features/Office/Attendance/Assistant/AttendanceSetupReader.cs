using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Organization;
using AiNexus.Features.Office.Attendance.Cards;
using AiNexus.Features.Office.Attendance.Records;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Assistant;

/// <summary>
/// 讀出勤名單的設定給助理（<see cref="AttendanceSetup"/>）。持卡的人走 <see cref="AttendanceRoster.HoldingsAsync"/>，與出勤紀錄同一套資料範圍，
/// 但含隱藏的卡、不限期間：要說明「為什麼不見」，就得看得到名單略過的那些設定。範圍外的人一樣查不到。
/// </summary>
[Scoped]
internal sealed class AttendanceSetupReader(AppDbContext db, IDataScope scopes, AttendanceRoster roster, OrganizationDirectory organization, IActiveUsers activeUsers)
{
    /// <summary>同名的人最多列幾位：再多就請使用者改用帳號或卡號。</summary>
    public const int MaxPeople = 5;

    /// <summary>
    /// 帳號、姓名或卡號完全相同的人（不分大小寫由資料庫定序決定，與名單的比對相同）：持卡的人連同他所有的持有期間，
    /// 以及範圍內沒有任何持卡紀錄的使用者（不在名單上的常見原因）。
    /// </summary>
    public async Task<IReadOnlyList<AttendanceSetup>> FindAsync(Guid viewer, string text, CancellationToken ct)
    {
        var scope = await scopes.ForAsync(viewer, FeatureCatalog.AttendanceRecords, ct);
        if (scope.IsEmpty) return [];

        var holdings = await roster.HoldingsAsync(viewer, scope, new(), null, includeHidden: true, ct);
        var rows = from a in holdings
                   join c in db.Cards on a.CardId equals c.Id
                   join u in db.Users on a.UserId equals u.Id into owners
                   from u in owners.DefaultIfEmpty()
                   select new
                   {
                       a.UserId, a.HolderName, DisplayName = u == null ? null : u.DisplayName, AdAccount = u == null ? null : u.AdAccount,
                       LocalAccount = u == null ? null : u.LocalAccount, c.CardNumber, Hidden = c.HiddenFromRecords, a.StartsAt, a.EndsAt, a.Note,
                   };
        var matched = rows.Where(x => x.CardNumber == text || x.HolderName == text || x.DisplayName == text || x.AdAccount == text || x.LocalAccount == text);
        var matchedUsers = matched.Where(x => x.UserId != null).Select(x => x.UserId);
        var read = await rows.Where(x => x.UserId != null && matchedUsers.Contains(x.UserId)).ToListAsync(ct);
        read.AddRange(await matched.Where(x => x.UserId == null).ToListAsync(ct));
        List<Row> found = [.. read.Select(x => new Row(x.UserId, x.HolderName, x.DisplayName, x.AdAccount ?? x.LocalAccount, x.CardNumber, x.Hidden, x.StartsAt, x.EndsAt, x.Note))];

        var users = db.Users.AsNoTracking().Where(x => x.DeletedAt == null && (x.DisplayName == text || x.AdAccount == text || x.LocalAccount == text));
        if (!scope.All)
        {
            var own = scope.Own;
            var members = organization.MemberIds(scope.UnitIds);
            users = users.Where(x => own && x.Id == viewer || members.Contains(x.Id));
        }
        var holders = holdings.Where(x => x.UserId != null).Select(x => x.UserId!.Value);
        var cardless = await users.Where(x => !holders.Contains(x.Id))
            .Select(x => new { x.Id, x.DisplayName, Account = x.AdAccount ?? x.LocalAccount }).Take(MaxPeople).ToListAsync(ct);

        List<Guid> ids = [.. found.Where(x => x.UserId is not null).Select(x => x.UserId!.Value).Concat(cardless.Select(x => x.Id)).Distinct()];
        var active = (await activeUsers.Ids.Where(x => ids.Contains(x)).ToListAsync(ct)).ToHashSet();

        List<AttendanceSetup> people =
        [
            .. found.GroupBy(x => (x.UserId, Card: x.UserId is null ? x.CardNumber : null, Name: x.UserId is null ? x.HolderName : null)).Select(g =>
            {
                var first = g.First();
                return new AttendanceSetup(g.Key.UserId, first.DisplayName ?? first.HolderName ?? "", first.Account,
                    g.Key.UserId is { } id ? active.Contains(id) : null, Holdings(g));
            }),
            .. cardless.Select(x => new AttendanceSetup(x.Id, x.DisplayName, x.Account, active.Contains(x.Id), [])),
        ];
        return [.. people.OrderBy(x => x.DisplayName, StringComparer.Ordinal).Take(MaxPeople)];
    }

    /// <summary>
    /// 帳號已停用、持有期間卻還沒結束的人（只含沒結束的那幾段）：出勤紀錄會一直把他列成未刷卡。
    /// 與 <see cref="CardAccountReview"/> 的「仍持有出勤卡」同一個條件；人數少，整份讀出來再由呼叫端與名單取交集，不把名單展開成參數。
    /// </summary>
    public async Task<IReadOnlyList<AttendanceSetup>> StillHeldAfterDisabledAsync(DateTimeOffset now, CancellationToken ct)
    {
        var active = activeUsers.Ids;
        var read = await (from a in db.CardAssignments.AsNoTracking().UnendedAt(now)
                          where a.UserId != null && !active.Contains(a.UserId.Value)
                          join c in db.Cards on a.CardId equals c.Id
                          join u in db.Users on a.UserId equals u.Id
                          select new { a.UserId, a.HolderName, u.DisplayName, Account = u.AdAccount ?? u.LocalAccount, c.CardNumber, c.HiddenFromRecords, a.StartsAt, a.EndsAt, a.Note })
            .ToListAsync(ct);
        var rows = read.Select(x => new Row(x.UserId, x.HolderName, x.DisplayName, x.Account, x.CardNumber, x.HiddenFromRecords, x.StartsAt, x.EndsAt, x.Note));
        return [.. rows.GroupBy(x => x.UserId!.Value).Select(g => new AttendanceSetup(g.Key, g.First().DisplayName ?? "", g.First().Account, false, Holdings(g)))];
    }

    private static List<AttendanceSetupHolding> Holdings(IEnumerable<Row> rows)
        => [.. rows.OrderBy(x => x.StartsAt).Select(x => new AttendanceSetupHolding(x.CardNumber, x.Hidden, x.StartsAt, x.EndsAt, x.Note))];

    private sealed record Row(Guid? UserId, string? HolderName, string? DisplayName, string? Account, string CardNumber, bool Hidden,
        DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string? Note);
}
