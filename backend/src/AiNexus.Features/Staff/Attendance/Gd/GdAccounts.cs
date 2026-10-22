using AiNexus.Data.App;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Staff.Attendance.Gd;

/// <summary>
/// 使用者對到的公文帳號（<c>Sys_User.ID</c>）。<paramref name="GdIds"/> 只含對得到的使用者，同一個 AD 帳號在 <c>Sys_User</c> 有多筆時全部列出；
/// <paramref name="Unmapped"/> 是對不到的使用者（沒有 AD 帳號、<c>Sys_User</c> 沒有、或使用者不存在），他們在 GD 的資料是未知，不是「沒有」。
/// </summary>
public sealed record GdAccountMap(IReadOnlyDictionary<Guid, IReadOnlyList<string>> GdIds, IReadOnlySet<Guid> Unmapped);

/// <summary>
/// 使用者與公文帳號的對應（ADR 0018）：<c>Users.AdAccount</c> 對 <c>Sys_User.ACCOUNT</c>。本系統不存公文帳號，AD 帳號改名時不必同步，所以每次現查。
/// 讀假單與回寫假日（換算操作者）共用；GD 不可用時回傳 <see langword="null"/>，不能當成所有人都對不到。
/// </summary>
public sealed class GdAccounts(AppDbContext db, GdAccess gd)
{
    private const string Sql = "SELECT [ID] AS GdId, [ACCOUNT] AS Account FROM [dbo].[Sys_User] WHERE [ACCOUNT] IN @Accounts";

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

        var gdIds = new Dictionary<Guid, List<string>>();
        foreach (var row in rows)
            foreach (var user in byAccount[row.Account.Trim()])
            {
                if (!gdIds.TryGetValue(user, out var list)) gdIds[user] = list = [];
                list.Add(row.GdId.Trim());
            }
        ids.ExceptWith(gdIds.Keys);
        return new(gdIds.ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Value), ids);
    }

    private sealed class AccountRow
    {
        public string GdId { get; init; } = "";
        public string Account { get; init; } = "";
    }
}
