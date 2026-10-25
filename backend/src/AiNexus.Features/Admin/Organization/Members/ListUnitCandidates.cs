using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization.Members;

/// <summary>可加入單位的一位使用者；<c>PrimaryUnitId</c> 是目前的主要所屬，沒有時為 null（還沒分派部門）。</summary>
public sealed record UnitCandidateDto(Guid Id, string Account, string DisplayName, Guid? PrimaryUnitId);

/// <summary>
/// 加入成員時可瀏覽挑選的全部在職使用者，依帳號排列；路徑由前端以已載入的單位組回。
/// 不分頁：使用者與單位同為百筆等級（ADR 0017），管理員要看到整份名單才挑得出人，不必記得姓名或帳號。
/// </summary>
internal sealed class ListUnitCandidates(AppDbContext db, IActiveUsers activeUsers)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/candidates", async (ListUnitCandidates handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
        .WithName("ListOrganizationUnitCandidates");

    public async Task<IReadOnlyList<UnitCandidateDto>> HandleAsync(CancellationToken ct)
        => await db.Users.AsNoTracking().Where(x => activeUsers.Ids.Contains(x.Id)).OrderBy(x => x.AdAccount ?? x.LocalAccount)
            .Select(x => new UnitCandidateDto(x.Id, x.AdAccount ?? x.LocalAccount ?? "", x.DisplayName,
                db.Set<UnitMember>().Where(y => y.UserId == x.Id && y.IsPrimary).Select(y => (Guid?)y.UnitId).FirstOrDefault()))
            .ToListAsync(ct);
}
