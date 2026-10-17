using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Knowledge.Collections;

/// <summary>使用者可讀取的知識庫，依最近更新排序，最多 100 筆，附文件數與已就緒文件數。</summary>
internal sealed class ListKnowledgeCollections(NexusDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/collections", async (ICurrentUser user, ListKnowledgeCollections handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("ListKnowledgeCollections");

    public async Task<IReadOnlyList<CollectionDto>> HandleAsync(Guid actor, CancellationToken ct)
    {
        var query = await access.QueryAsync(actor, KnowledgeCollection.Kind, ct);
        return await (from resource in query.AsNoTracking() join collection in db.Set<KnowledgeCollection>() on resource.Id equals collection.Id orderby resource.UpdatedAt descending
            select new CollectionDto(new(resource.Id, resource.Name, resource.Kind, resource.OwnerId == actor || db.Set<ResourceMember>().Any(m => m.ResourceId == resource.Id && m.UserId == actor && m.Role == "editor"), resource.OwnerId == actor, resource.UpdatedAt), collection.Description,
                db.Set<KnowledgeDocument>().Count(d => d.CollectionId == resource.Id && !d.IsDeleted), db.Set<KnowledgeDocument>().Count(d => d.CollectionId == resource.Id && !d.IsDeleted && d.Status == "ready"))).Take(100).ToListAsync(ct);
    }
}
