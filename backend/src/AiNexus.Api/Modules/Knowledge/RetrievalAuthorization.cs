using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Collaboration;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Knowledge;

public sealed class RetrievalAuthorization(NexusDbContext db, ResourceAccess access, AccessService features)
{
    public async Task CollectionsAsync(Guid actor, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count > 3 || ids.Distinct().Count() != ids.Count) throw new ApiException(400, "knowledge_collection_limit", "每次最多使用三個不同知識庫。");
        if (ids.Count > 0 && !(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "knowledge")) throw new ApiException(403, "knowledge_access_revoked", "知識庫功能權限已撤銷，請重新載入對話。");
        foreach (var id in ids) await access.RequireAsync(actor, id, "knowledge", ct);
    }
    public async Task HitsAsync(Guid actor, IReadOnlyList<KnowledgeHitDto> hits, CancellationToken ct)
    {
        if (hits.Count == 0) return;
        var ids = hits.Select(x => x.DocumentId).Distinct().ToArray();
        var docs = await db.Set<KnowledgeDocument>().AsNoTracking().Where(x => ids.Contains(x.Id) && !x.IsDeleted && x.Status == "ready" && x.CollectionId != null)
            .Select(x => new { x.Id, Collection = x.CollectionId!.Value }).ToListAsync(ct);
        if (docs.Count != ids.Length) throw new ApiException(409, "knowledge_source_changed", "知識來源已改變，請重新送出提問。");
        await CollectionsAsync(actor, docs.Select(x => x.Collection).Distinct().ToArray(), ct);
    }
}
