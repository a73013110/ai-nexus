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
    /// 確認呼叫者可使用這些知識庫。<paramref name="fresh"/> 會重新讀取本次請求已讀過的授權，用於遠端呼叫之後的再次檢查，
    /// 確保呼叫期間被撤銷的權限仍會生效。
    /// </summary>
    public async Task<Result> CollectionsAsync(Guid actor, IReadOnlyList<Guid> ids, CancellationToken ct, bool fresh = false)
    {
        if (ids.Count > 3 || ids.Distinct().Count() != ids.Count) return KnowledgeErrors.SelectionLimit;
        if (ids.Count == 0) return Result.Success;
        if (fresh) features.Invalidate();
        if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "knowledge")) return AccessControlErrors.FeatureForbidden;
        return await access.RequireAllAsync(actor, ids, "knowledge", ct);
    }

    /// <summary>
    /// 命中的文件仍為就緒，且位於呼叫者可使用的知識庫中。<paramref name="authorized"/> 內的知識庫已在同一次搜尋中檢查過，這裡不重複檢查。
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
