using AiNexus.Data.App;
using AiNexus.Data.App.Artifacts;
using AiNexus.Data.App.Collaboration;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Artifacts.Management;

public sealed record ArtifactSummaryDto(ResourceDto Resource, int Version, Guid? ProjectId);

/// <summary>使用者可讀取、依更新時間排序的前 200 份成果，並標示每份是否可編輯。</summary>
internal sealed class ListArtifacts(AppDbContext db, ResourceAccess access)
{
    private const int PageSize = 200;

    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("", async (ICurrentUser user, ListArtifacts handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("ListArtifacts");

    public async Task<IReadOnlyList<ArtifactSummaryDto>> HandleAsync(Guid actor, CancellationToken ct)
    {
        var query = await access.QueryAsync(actor, Artifact.Kind, ct);
        // 可編輯條件：擁有者、具編輯角色的成員，或目前父專案的編輯者（即時計算，不是快照）。
        return await (from resource in query.AsNoTracking()
                join item in db.Set<Artifact>() on resource.Id equals item.Id
                orderby resource.UpdatedAt descending
                select new ArtifactSummaryDto(
                    new(resource.Id, resource.Name, resource.Kind,
                        resource.OwnerId == actor
                        || db.Set<ResourceMember>().Any(m => m.ResourceId == item.Id && m.UserId == actor && m.Role == "editor")
                        || db.Set<Resource>().Any(p => p.Id == resource.ParentId
                            && (p.OwnerId == actor
                                || db.Set<ResourceMember>().Any(m => m.ResourceId == p.Id && m.UserId == actor && m.Role == "editor"))),
                        resource.OwnerId == actor,
                        resource.UpdatedAt),
                    item.Version,
                    item.ProjectId))
            .Take(PageSize)
            .ToListAsync(ct);
    }
}
