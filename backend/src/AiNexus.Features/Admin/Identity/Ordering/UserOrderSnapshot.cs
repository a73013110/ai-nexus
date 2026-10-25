using AiNexus.Data.App;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Ordering;

/// <summary>排序變更的稽核快照：<c>[{ id, sortOrder }]</c>（JSON 形狀即稽核合約）。</summary>
internal static class UserOrderSnapshot
{
    /// <summary><paramref name="ids"/> 為 null 時取所有已排序的人（清除排序用）。</summary>
    public static async Task<object?> OfAsync(AppDbContext db, IReadOnlyCollection<Guid>? ids, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking();
        users = ids is null ? users.Where(x => x.SortOrder != null) : users.Where(x => ids.Contains(x.Id));
        return await users.OrderBy(x => x.Id).Select(x => new { x.Id, x.SortOrder }).ToArrayAsync(ct);
    }
}
