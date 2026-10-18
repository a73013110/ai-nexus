using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using System.Text.RegularExpressions;
using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>
/// 選單群組與功能掛載位置的格式規則。與 <see cref="AccessRules"/> 相同，在稽核交易內執行，被拒絕的編輯仍會留下錯誤代碼的稽核紀錄。
/// </summary>
internal static class NavigationRules
{
    public const int MaxSortOrder = 10000;

    public static Error? Order(int sortOrder) => sortOrder is < 0 or > MaxSortOrder ? AccessControlErrors.InvalidOrder : null;

    public static Error? Icon(string icon)
        => Regex.IsMatch(icon, "^[a-z][a-z0-9-]{0,39}$", RegexOptions.CultureInvariant) ? null : AccessControlErrors.InvalidFeatureIcon;

    /// <summary>null 代表頂層；其他值必須指向既有群組。</summary>
    public static async Task<Error?> ExistingGroupAsync(AppDbContext db, string? id, CancellationToken ct)
        => id is null || await db.Set<NavigationGroup>().AnyAsync(x => x.Id == id, ct) ? null : AccessControlErrors.NavigationGroupNotFound;

    /// <summary>新增、更新與刪除選單群組共用的稽核快照（<c>admin.navigation_group*</c>）。</summary>
    public static async Task<object?> SnapshotAsync(AppDbContext db, string id, CancellationToken ct)
    {
        var group = await db.Set<NavigationGroup>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return group is null ? null : new { group.Name, group.ParentId, group.SortOrder };
    }
}
