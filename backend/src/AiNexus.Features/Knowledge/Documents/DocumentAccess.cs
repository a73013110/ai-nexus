using AiNexus.Data.App;
using AiNexus.Data.App.Attachments;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>
/// 決定誰可讀取或變更文件。知識庫文件沿用所屬知識庫的 ACL，且需開通知識庫功能；獨立文件沿用自己的資源權限，
/// 若屬於專案則再加上專案的 ACL。找不到文件或功能未開通時一律回傳 <see cref="KnowledgeErrors.DocumentNotFound"/>，
/// 不區分兩者，以免洩漏文件是否存在；ACL 失敗則回傳 <see cref="ResourceAccess"/> 的錯誤。
/// </summary>
internal sealed class DocumentAccess(AppDbContext db, ResourceAccess access, AccessService features)
{
    /// <summary>以追蹤模式載入文件並完成權限檢查，供呼叫端修改。</summary>
    public async Task<Result<Document>> FindAsync(Guid actor, Guid id, CancellationToken ct, bool write = false)
    {
        var doc = await db.Set<Document>().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (doc is null) return KnowledgeErrors.DocumentNotFound;
        if (doc.CollectionId is Guid collection)
        {
            if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "knowledge")) return KnowledgeErrors.DocumentNotFound;
            var granted = await access.RequireAsync(actor, collection, Collection.Kind, ct, write);
            if (!granted.IsSuccess) return granted.Error;
        }
        else
        {
            if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id is "chat" or "knowledge" or "projects")) return KnowledgeErrors.DocumentNotFound;
            var resource = await access.RequireAsync(actor, doc.Id, Document.Kind, ct, write);
            if (!resource.IsSuccess) return resource.Error;
            if (resource.Value.ParentId is Guid parent)
            {
                if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "projects")) return KnowledgeErrors.DocumentNotFound;
                var project = await access.RequireAsync(actor, parent, "project", ct, write);
                if (!project.IsSuccess) return project.Error;
            }
        }
        return doc;
    }

    public async Task<Result<DocumentDto>> DetailAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var document = await FindAsync(actor, id, ct);
        if (!document.IsSuccess) return document.Error;
        return await DescribeAsync(actor, document.Value, ct);
    }

    /// <summary>
    /// <see cref="DetailAsync"/> 的批次版本：結果順序與輸入相同，檢查與失敗規則也相同，且查詢次數固定。
    /// 任一文件失敗即整批失敗。
    /// </summary>
    public async Task<Result<IReadOnlyList<DocumentDto>>> DetailsAsync(Guid actor, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return Array.Empty<DocumentDto>();
        var docs = await db.Set<Document>().AsNoTracking().Where(x => ids.Contains(x.Id) && !x.IsDeleted).ToDictionaryAsync(x => x.Id, ct);
        var granted = (await features.ForUserAsync(actor, ct)).Features.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var collections = docs.Values.Where(x => x.CollectionId != null).Select(x => x.CollectionId!.Value).Distinct().ToArray();
        var standalone = docs.Values.Where(x => x.CollectionId == null).Select(x => x.Id).ToArray();
        // DescribeAsync 回報權限所依據的資源：知識庫文件看所屬知識庫，獨立文件看它自己。
        var readable = new Dictionary<Guid, Resource>();
        if (collections.Length > 0)
            foreach (var item in await (await access.QueryAsync(actor, Collection.Kind, ct)).AsNoTracking().Where(x => collections.Contains(x.Id)).ToListAsync(ct)) readable[item.Id] = item;
        if (standalone.Length > 0)
            foreach (var item in await (await access.QueryAsync(actor, Document.Kind, ct)).AsNoTracking().Where(x => standalone.Contains(x.Id)).ToListAsync(ct)) readable[item.Id] = item;
        var parents = readable.Values.Where(x => x.Kind == Document.Kind && x.ParentId != null).Select(x => x.ParentId!.Value).Distinct().ToArray();
        var projects = parents.Length == 0 ? [] : await (await access.QueryAsync(actor, "project", ct)).Where(x => parents.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        var resources = new List<Resource>(ids.Count);
        foreach (var id in ids)
        {
            if (!docs.TryGetValue(id, out var doc)) return KnowledgeErrors.DocumentNotFound;
            if (doc.CollectionId is Guid collection)
            {
                if (!granted.Contains("knowledge")) return KnowledgeErrors.DocumentNotFound;
                if (readable.GetValueOrDefault(collection) is not { } readableCollection) return CollaborationErrors.ResourceNotFound;
                resources.Add(readableCollection);
            }
            else
            {
                if (!granted.Contains("chat") && !granted.Contains("knowledge") && !granted.Contains("projects")) return KnowledgeErrors.DocumentNotFound;
                if (readable.GetValueOrDefault(doc.Id) is not { } resource) return CollaborationErrors.ResourceNotFound;
                if (resource.ParentId is Guid parent)
                {
                    if (!granted.Contains("projects")) return KnowledgeErrors.DocumentNotFound;
                    if (!projects.Contains(parent)) return CollaborationErrors.ResourceNotFound;
                }
                resources.Add(resource);
            }
        }
        var editable = await access.EditableAsync(actor, resources.DistinctBy(x => x.Id).ToArray(), ct);
        var jobs = docs.Values.Where(x => x.JobId != null).Select(x => x.JobId!.Value).Distinct().ToArray();
        var states = jobs.Length == 0 ? [] : await db.Set<BackgroundJob>().Where(x => jobs.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Status, ct);
        return ids.Select((id, index) => Describe(docs[id], editable.Contains(resources[index].Id), docs[id].JobId is Guid job ? states.GetValueOrDefault(job) : null)).ToArray();
    }

    /// <summary>取得呼叫者可讀取文件的原始附件；附件必須已就緒（儲存狀態為 Ready）。</summary>
    public async Task<Result<Attachment>> OriginalAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var document = await FindAsync(actor, id, ct);
        if (!document.IsSuccess) return document.Error;
        if (document.Value.AttachmentId is not Guid attachment) return KnowledgeErrors.DocumentNotFound;
        var file = await db.Set<Attachment>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachment && x.StorageState == AttachmentStates.Ready, ct);
        if (file is null) return KnowledgeErrors.DocumentNotFound;
        return file;
    }

    public async Task<Result<DocumentDto>> DescribeAsync(Guid actor, Document doc, CancellationToken ct)
    {
        var resource = await access.RequireAsync(actor, doc.CollectionId ?? doc.Id, doc.CollectionId is null ? Document.Kind : Collection.Kind, ct);
        if (!resource.IsSuccess) return resource.Error;
        var editable = (await access.DescribeAsync(actor, resource.Value, ct)).CanEdit;
        var state = doc.JobId is Guid job ? await db.Set<BackgroundJob>().Where(x => x.Id == job).Select(x => x.Status).SingleOrDefaultAsync(ct) : null;
        return Describe(doc, editable, state);
    }

    /// <summary>已就緒的文件一律顯示為就緒；否則以目前工作的狀態為準，覆蓋儲存的狀態。</summary>
    public static DocumentDto Describe(Document x, bool edit, string? jobStatus = null) => new(x.Id, x.CollectionId, x.FileName, x.ContentType, x.Status == "ready" ? "ready" : jobStatus ?? x.Status, x.PageCount, x.ChunkCount, x.Warning, x.JobId, edit, x.AttachmentId != null, x.TextVersion);
}
