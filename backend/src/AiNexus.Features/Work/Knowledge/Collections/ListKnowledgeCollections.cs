using AiNexus.Data.App;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Knowledge.Collections;

/// <summary>使用者可讀取的知識庫，依最近更新排序，最多 100 筆，附文件數與已就緒文件數。</summary>
internal sealed class ListKnowledgeCollections(AppDbContext db, ResourceAccess access) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/collections", (ICurrentUser user, ListKnowledgeCollections handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, ct))
        .Audited(KnowledgeAudit.CollectionRead)
        .WithName("ListKnowledgeCollections");

    public async Task<IReadOnlyList<CollectionDto>> HandleAsync(Guid actor, CancellationToken ct)
    {
        var query = await access.QueryAsync(actor, Collection.Kind, ct);
        return await (from resource in query.AsNoTracking() join collection in db.Collections on resource.Id equals collection.Id orderby resource.UpdatedAt descending
            select new CollectionDto(new(resource.Id, resource.Name, resource.Kind, resource.OwnerId == actor || db.ResourceMembers.Any(m => m.ResourceId == resource.Id && m.UserId == actor && m.Role == "editor"), resource.OwnerId == actor, resource.UpdatedAt), collection.Description,
                db.Documents.Count(d => d.CollectionId == resource.Id && !d.IsDeleted), db.Documents.Count(d => d.CollectionId == resource.Id && !d.IsDeleted && d.Status == "ready"))).Take(100).ToListAsync(ct);
    }
}
