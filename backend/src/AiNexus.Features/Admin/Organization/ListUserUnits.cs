using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization;

/// <summary>使用者所屬的一個單位；<c>Path</c> 是由最上層到該單位的名稱，讓同名的下層單位（例如各處的「行政課」）分得出來。</summary>
public sealed record UserUnitDto(Guid UnitId, IReadOnlyList<string> Path, bool Enabled, bool IsPrimary, bool IsManager);

/// <summary>
/// 使用者管理頁顯示的所屬單位（唯讀），主要所屬在前。放在 Organization 而不是 Identity：Identity 是下層模組，不能讀組織資料。
/// 使用者不存在時回空清單，與使用者頁本身的 404 不重複。
/// </summary>
internal sealed class ListUserUnits(AppDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/users/{id:guid}/organization-units", async (Guid id, ListUserUnits handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(id, ct)))
        .WithName("ListUserOrganizationUnits");

    public async Task<IReadOnlyList<UserUnitDto>> HandleAsync(Guid userId, CancellationToken ct)
    {
        var memberships = await db.Set<UnitMember>().AsNoTracking().Where(x => x.UserId == userId).ToListAsync(ct);
        if (memberships.Count == 0) return [];
        // 單位表很小（部門數以百計），整表讀進來組路徑比遞迴 CTE 簡單。
        var units = await db.Set<Unit>().AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        return [.. memberships
            .Select(x => new UserUnitDto(x.UnitId, Path(units, x.UnitId), units[x.UnitId].Enabled, x.IsPrimary, x.IsManager))
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => string.Join('\u0001', x.Path), StringComparer.CurrentCulture)];
    }

    private static string[] Path(IReadOnlyDictionary<Guid, Unit> units, Guid id)
        => [.. UnitRules.SelfAndAncestors(units, id).Reverse().Select(x => x.Name)];
}
