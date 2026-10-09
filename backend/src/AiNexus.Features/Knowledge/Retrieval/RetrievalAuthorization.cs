using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Knowledge.Retrieval;

public sealed class RetrievalAuthorization(NexusDbContext db, ResourceAccess access, AccessService features)
{
    /// <summary>
    /// The actor may use these collections. <paramref name="fresh"/> re-reads grants already read in this request: use it for
    /// a re-check after a remote call, so a revocation during the call still applies.
    /// </summary>
    public async Task CollectionsAsync(Guid actor, IReadOnlyList<Guid> ids, CancellationToken ct, bool fresh = false)
    {
        if (ids.Count > 3 || ids.Distinct().Count() != ids.Count) throw new ApiException(400, "knowledge_collection_limit", "每次最多使用三個不同知識庫。");
        if (ids.Count == 0) return;
        if (fresh) features.Invalidate();
        if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "knowledge")) throw new ApiException(403, "knowledge_access_revoked", "知識庫功能權限已撤銷，請重新載入對話。");
        await access.RequireAllAsync(actor, ids, "knowledge", ct);
    }

    /// <summary>
    /// The hits' documents are still ready in collections the actor may use. Collections in <paramref name="authorized"/>
    /// were checked earlier in the same search and are not checked again.
    /// </summary>
    public async Task HitsAsync(Guid actor, IReadOnlyList<KnowledgeHitDto> hits, CancellationToken ct, IReadOnlyCollection<Guid>? authorized = null)
    {
        if (hits.Count == 0) return;
        var ids = hits.Select(x => x.DocumentId).Distinct().ToArray();
        var docs = await db.Set<KnowledgeDocument>().AsNoTracking().Where(x => ids.Contains(x.Id) && !x.IsDeleted && x.Status == "ready" && x.CollectionId != null)
            .Select(x => new { x.Id, Collection = x.CollectionId!.Value }).ToListAsync(ct);
        if (docs.Count != ids.Length) throw new ApiException(409, "knowledge_source_changed", "知識來源已改變，請重新送出提問。");
        var collections = docs.Select(x => x.Collection).Distinct().ToArray();
        if (authorized is not null && collections.All(authorized.Contains)) return;
        await CollectionsAsync(actor, collections, ct);
    }
}
