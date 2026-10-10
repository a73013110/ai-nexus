using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Knowledge.Collections;

/// <summary>Up to 100 collections the user may read, most recently changed first, with their document counts.</summary>
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
