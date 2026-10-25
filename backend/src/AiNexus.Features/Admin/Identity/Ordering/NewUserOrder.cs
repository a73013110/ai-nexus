using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Ordering;

/// <summary>
/// 新使用者（管理員新增、從 AD 匯入、AD 首次登入）接在手動順序的最後一位已排序者之後，不混進依姓名排列的未排序區。
/// 還沒有人排序時維持未排序：給值會讓新人變成唯一已排序的人，反而排到最前面。
/// 值用 <see cref="ManualOrder.Plan"/> 算（與拖曳同一套規則），上限不夠時會一併重新編號已排序的人。
/// 同時新增的兩人可能拿到同一個值，同值時依姓名排，不會出錯。
/// </summary>
internal static class NewUserOrder
{
    /// <summary>設定 <paramref name="user"/> 的排序值（不儲存）；呼叫端已把它加進追蹤，負責交易與儲存。</summary>
    public static async Task AppendAsync(AppDbContext db, User user, CancellationToken ct)
    {
        var sorted = await db.Users.AsNoTracking()
            .Where(x => x.DeletedAt == null && x.SortOrder != null && x.Id != user.Id)
            .Select(x => new { x.Id, x.SortOrder, x.DisplayName, x.Account })
            .ToListAsync(ct);
        if (sorted.Count == 0) return;
        var ordered = sorted.InUserOrder(x => x.SortOrder, x => x.DisplayName, x => x.Account).ThenBy(x => x.Id)
            .Select(x => (x.Id, x.SortOrder)).ToList();
        var writes = ManualOrder.Plan(ordered, user.Id, ordered.Count, UserOrder.MaxSortOrder);
        var others = writes.Where(x => x.Id != user.Id).Select(x => x.Id).ToArray();
        var renumbered = others.Length == 0 ? new Dictionary<Guid, User>() : await db.Users.Where(x => others.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        foreach (var (id, sortOrder) in writes) (id == user.Id ? user : renumbered[id]).SortOrder = sortOrder;
    }
}
