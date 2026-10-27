using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Platform.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization;

/// <summary>組織管理頁的全部單位（含停用）與直屬成員數；依同層排序、名稱排列，樹由前端依 <c>ParentId</c> 組回。</summary>
internal sealed class ListUnits(AppDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", async (ListUnits handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
        .Audited(OrganizationAudit.UnitRead)
        .WithName("ListOrganizationUnits");

    public async Task<IReadOnlyList<UnitDto>> HandleAsync(CancellationToken ct)
    {
        var units = await db.Set<Unit>().AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(ct);
        var counts = await db.Set<UnitMember>().AsNoTracking().GroupBy(x => x.UnitId).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return [.. units.Select(x => UnitDto.From(x, counts.GetValueOrDefault(x.Id)))];
    }
}
