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
    public async Task<Result> CollectionsAsync(Guid actor, IReadOnlyList<Guid> ids, CancellationToken ct, bool fresh = false)
    {
        if (ids.Count > 3 || ids.Distinct().Count() != ids.Count) return KnowledgeErrors.SelectionLimit;
        if (ids.Count == 0) return Result.Success;
        if (fresh) features.Invalidate();
        if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "knowledge")) return KnowledgeErrors.AccessRevoked;
        return await access.RequireAllAsync(actor, ids, "knowledge", ct);
    }

    /// <summary>
    /// The hits' documents are still ready in collections the actor may use. Collections in <paramref name="authorized"/>
    /// were checked earlier in the same search and are not checked again.
    /// </summary>
    public async Task<Result> HitsAsync(Guid actor, IReadOnlyList<KnowledgeHitDto> hits, CancellationToken ct, IReadOnlyCollection<Guid>? authorized = null)
    {
        if (hits.Count == 0) return Result.Success;
        var ids = hits.Select(x => x.DocumentId).Distinct().ToArray();
        var docs = await db.Set<KnowledgeDocument>().AsNoTracking().Where(x => ids.Contains(x.Id) && !x.IsDeleted && x.Status == "ready" && x.CollectionId != null)
            .Select(x => new { x.Id, Collection = x.CollectionId!.Value }).ToListAsync(ct);
        if (docs.Count != ids.Length) return KnowledgeErrors.SourceChanged;
        var collections = docs.Select(x => x.Collection).Distinct().ToArray();
        if (authorized is not null && collections.All(authorized.Contains)) return Result.Success;
        return await CollectionsAsync(actor, collections, ct);
    }
}
