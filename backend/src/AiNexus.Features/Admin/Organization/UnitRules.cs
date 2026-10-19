using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Platform.Errors;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization;

/// <summary>
/// 單位樹需要查資料的規則，在 <c>AdministrativeAudit</c> 的交易內執行，被拒絕的變更仍會留下錯誤代碼的稽核。
/// 結構規則一律套在「變更後的完整單位清單」上檢查，新增、修改與排序共用同一套判斷。
/// </summary>
internal static class UnitRules
{
    /// <summary>集團 › 公司 › 處 › 部 › 課 › 組之外再留兩層餘裕；更深的樹通常是把專案或職務誤建成單位。</summary>
    public const int MaxDepth = 8;
    public const int MaxSortOrder = 10000, SortStep = 10, MaxSiblings = MaxSortOrder / SortStep;

    /// <summary>上層必須存在、不可成環、深度不超過 <see cref="MaxDepth"/>、同一層名稱不可重複（資料庫唯一索引的提前檢查，才能回固定代碼並寫稽核）。</summary>
    public static Error? Structure(IReadOnlyCollection<Unit> units)
    {
        var parents = units.ToDictionary(x => x.Id, x => x.ParentId);
        if (units.Any(x => x.ParentId is { } parent && !parents.ContainsKey(parent))) return OrganizationErrors.ParentInvalid;
        var deepest = 0;
        foreach (var unit in units)
        {
            var depth = 1;
            for (var at = unit.ParentId; at is { } current; at = parents[current])
            {
                // 走超過單位總數仍未到頂，代表有環。
                if (current == unit.Id || ++depth > units.Count) return OrganizationErrors.UnitCycle;
            }
            deepest = Math.Max(deepest, depth);
        }
        if (deepest > MaxDepth) return OrganizationErrors.DepthExceeded;
        // 資料庫定序不分大小寫，這裡比照；全形半形等其他差異仍由唯一索引擋下。
        return units.GroupBy(x => (x.ParentId, Name: x.Name.ToUpperInvariant())).Any(x => x.Count() > 1) ? OrganizationErrors.NameDuplicate : null;
    }

    /// <summary>排到 <paramref name="parentId"/> 這一層的最後；已排到上限時與最後一個同值，再依名稱排，不因此拒絕。</summary>
    public static int NextSortOrder(IEnumerable<Unit> units, Guid? parentId)
        => Math.Min((units.Where(x => x.ParentId == parentId).Select(x => (int?)x.SortOrder).Max() ?? 0) + SortStep, MaxSortOrder);

    /// <summary>新增、修改與刪除單位共用的稽核快照（<c>admin.organization_unit*</c>）。</summary>
    public static async Task<object?> SnapshotAsync(AppDbContext db, Guid id, CancellationToken ct)
        => await db.Set<Unit>().AsNoTracking().Where(x => x.Id == id).Select(x => new { x.ParentId, x.Name, x.Enabled, x.SortOrder }).SingleOrDefaultAsync(ct);

    /// <summary>
    /// 一位使用者的全部所屬，給成員異動的稽核快照（<c>admin.organization_member*</c>）：設成主要所屬會同時改掉另一個單位的列，只看單一列會漏掉。
    /// </summary>
    public static async Task<object?> MembershipsAsync(AppDbContext db, Guid userId, CancellationToken ct)
        => new
        {
            userId,
            memberships = await db.Set<UnitMember>().AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.UnitId)
                .Select(x => new { x.UnitId, x.IsPrimary, x.IsManager }).ToArrayAsync(ct),
        };

    /// <summary>單位名稱的格式：去掉前後空白後 1～120 字、不含控制字元。</summary>
    public static IRuleBuilderOptions<T, string> UnitName<T>(this IRuleBuilder<T, string> rule)
        => rule.Must(x => !string.IsNullOrWhiteSpace(x)).WithErrorCode("required")
            .Must(x => x is null || x.Trim().Length <= Unit.NameMaxLength).WithErrorCode("too_long")
            .Must(x => x is null || !x.Any(char.IsControl)).WithErrorCode("format");
}
