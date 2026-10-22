using AiNexus.Data.App;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Integrations.Gd;

/// <summary>
/// 使用者對到的公文帳號（<c>Sys_User.ID</c>）。<paramref name="GdIds"/> 只含對得到的使用者；同一個 AD 帳號在 <c>Sys_User</c> 有多筆時只列候選（見 <see cref="GdAccounts"/>），候選仍有多筆就全部列出，由呼叫端決定。
/// <paramref name="Unmapped"/> 是對不到的使用者（沒有 AD 帳號、<c>Sys_User</c> 沒有、或使用者不存在），他們在 GD 的資料是未知，不是「沒有」。
/// </summary>
public sealed record GdAccountMap(IReadOnlyDictionary<Guid, IReadOnlyList<string>> GdIds, IReadOnlySet<Guid> Unmapped);

/// <summary>公文帳號對不到使用者的原因（反向對應 <see cref="GdAccounts.UsersOfAsync"/>）。</summary>
public enum GdAccountIssue
{
    /// <summary><c>Sys_User</c> 沒有這個 ID。</summary>
    NotInGd,

    /// <summary>公文帳號已停用（<c>Disable</c>=1）：人多半已離職或換了帳號，不當成現職的人。</summary>
    Disabled,

    /// <summary>公文帳號沒有 <c>ACCOUNT</c>，或沒有未刪除的使用者的 AD 帳號與它相同（還沒登入過平台）。</summary>
    NoUser,

    /// <summary>不只一位未刪除的使用者對到同一個 AD 帳號（大小寫、前後空白不同）；不猜。</summary>
    Ambiguous,
}

/// <summary>一個公文帳號（<c>Sys_User.ID</c>，已去空白）對到的使用者；對不到時 <paramref name="UserId"/> 為 null、<paramref name="Issue"/> 是原因。</summary>
public sealed record GdUserMatch(string GdId, Guid? UserId, GdAccountIssue? Issue);

/// <summary>
/// 使用者與公文帳號的對應（ADR 0018）：<c>Users.AdAccount</c> 對 <c>Sys_User.ACCOUNT</c>。本系統不存公文帳號，AD 帳號改名時不必同步，所以每次現查。
/// 讀假單與回寫假日（換算操作者）用使用者查公文帳號；組織匯入（部門主管）反過來用公文帳號查使用者（<see cref="UsersOfAsync"/>）。
/// GD 不可用時回傳 <see langword="null"/>（或失敗原因），不能當成所有人都對不到。
/// </summary>
public sealed class GdAccounts(AppDbContext db, GdAccess gd)
{
    private const string Sql = "SELECT [ID] AS GdId, [ACCOUNT] AS Account, [Disable] AS Disabled FROM [dbo].[Sys_User] WHERE [ACCOUNT] IN @Accounts";
    private const string ByIdSql = "SELECT [ID] AS GdId, [ACCOUNT] AS Account, [Disable] AS Disabled FROM [dbo].[Sys_User] WHERE [ID] IN @Ids";

    public async Task<GdAccountMap?> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.ToHashSet();
        var users = ids.Count == 0 ? [] : await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id) && x.AdAccount != null)
            .Select(x => new { x.Id, x.AdAccount }).ToListAsync(ct);
        // SQL 比對不分大小寫、忽略尾端空白，對回使用者時也要一樣寬鬆，否則查到的列會對不回人。
        var byAccount = users.Where(x => !string.IsNullOrWhiteSpace(x.AdAccount))
            .ToLookup(x => x.AdAccount!.Trim(), x => x.Id, StringComparer.OrdinalIgnoreCase);

        var rows = byAccount.Count == 0 ? [] : await gd.QueryInAsync<AccountRow>(Sql, byAccount.Select(x => x.Key), batch => new { Accounts = batch }, ct);
        if (rows is null) return null;

        var gdIds = new Dictionary<Guid, IReadOnlyList<string>>();
        foreach (var account in rows.GroupBy(x => x.Account!.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var candidates = Candidates(account.ToList());
            foreach (var user in byAccount[account.Key]) gdIds[user] = candidates;
        }
        ids.ExceptWith(gdIds.Keys);
        return new(gdIds, ids);
    }

    /// <summary>
    /// 反向對應：公文帳號 → <c>Sys_User.ACCOUNT</c> → 未刪除使用者的 <c>Users.AdAccount</c>（去空白、不分大小寫）。每個不重複的 ID（去空白、不分大小寫）回一筆，
    /// 依輸入的先後。停用的公文帳號不對應：部門資料可能還掛著離職者，把他當主管會讓他取得主管範圍。帳號停用（未刪除）的使用者照樣對應，由組織的有效成員規則排除。
    /// </summary>
    public async Task<GdRead<GdUserMatch>> UsersOfAsync(IEnumerable<string> gdIds, CancellationToken ct)
    {
        var ids = gdIds.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0) return new([], null);
        var read = await gd.ReadInAsync<AccountRow>(ByIdSql, ids, batch => new { Ids = batch }, ct);
        if (read.Rows is not { } rows) return new(null, read.Failure);

        var byId = rows.GroupBy(x => x.GdId.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var accounts = byId.Values.Where(x => x.Disabled != true && !string.IsNullOrWhiteSpace(x.Account)).Select(x => x.Account!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var users = accounts.Count == 0 ? [] : await db.Users.AsNoTracking().Where(x => x.DeletedAt == null && x.AdAccount != null && accounts.Contains(x.AdAccount))
            .Select(x => new { x.Id, x.AdAccount }).ToListAsync(ct);
        var byAccount = users.ToLookup(x => x.AdAccount!.Trim(), x => x.Id, StringComparer.OrdinalIgnoreCase);

        return new([.. ids.Select(Match)], null);

        GdUserMatch Match(string id)
        {
            if (!byId.TryGetValue(id, out var row)) return new(id, null, GdAccountIssue.NotInGd);
            if (row.Disabled == true) return new(id, null, GdAccountIssue.Disabled);
            var matches = string.IsNullOrWhiteSpace(row.Account) ? [] : byAccount[row.Account.Trim()].ToList();
            return matches switch
            {
                [var user] => new(id, user, null),
                [] => new(id, null, GdAccountIssue.NoUser),
                _ => new(id, null, GdAccountIssue.Ambiguous),
            };
        }
    }

    // 人員換新帳號時 GD 會留下停用的舊列，同一個 AD 帳號因此對到多筆；只看啟用列，這種人才對得到唯一帳號（回寫假日要求恰好一筆）。
    // 代價是有啟用列時，掛在停用舊帳號下的假單不再讀取。
    // 沒有任何啟用列（人已離職或停用）時退回全部列，否則已停用人員的歷史假單會查不到。Disable 為 null 視為啟用。
    private static IReadOnlyList<string> Candidates(IReadOnlyList<AccountRow> rows)
    {
        var enabled = rows.Where(x => x.Disabled != true).ToList();
        return [.. (enabled.Count > 0 ? enabled : rows).Select(x => x.GdId.Trim())];
    }

    private sealed class AccountRow
    {
        public string GdId { get; init; } = "";
        // 以 ACCOUNT 查詢時一定有值；以 ID 查詢時可能是 NULL。
        public string? Account { get; init; }
        public bool? Disabled { get; init; }
    }
}
