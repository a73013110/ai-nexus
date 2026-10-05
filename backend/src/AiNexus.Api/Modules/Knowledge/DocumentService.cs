using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Collaboration;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Knowledge;

public sealed class DocumentService(NexusDbContext db, ResourceAccess access, AccessService features, AttachmentService attachments, AttachmentWriteLock writes, JobService jobs, IOptions<KnowledgeOptions> options, AttachmentLifecycle lifecycle)
{
    public async Task<IReadOnlyList<CollectionDto>> CollectionsAsync(Guid actor, CancellationToken ct)
    {
        var query = await access.QueryAsync(actor, "knowledge", ct);
        return await (from resource in query.AsNoTracking() join collection in db.Set<KnowledgeCollection>() on resource.Id equals collection.Id orderby resource.UpdatedAt descending
            select new CollectionDto(new(resource.Id, resource.Name, resource.Kind, resource.OwnerId == actor || db.Set<ResourceMember>().Any(m => m.ResourceId == resource.Id && m.UserId == actor && m.Role == "editor"), resource.OwnerId == actor, resource.UpdatedAt), collection.Description,
                db.Set<KnowledgeDocument>().Count(d => d.CollectionId == resource.Id && !d.IsDeleted), db.Set<KnowledgeDocument>().Count(d => d.CollectionId == resource.Id && !d.IsDeleted && d.Status == "ready"))).Take(100).ToListAsync(ct);
    }
    public async Task<CollectionDto> CreateCollectionAsync(Guid actor, CollectionRequest request, CancellationToken ct)
    {
        var name = ResourceAccess.Name(request.Name);
        if (request.Description.Length > 2000) throw new ApiException(400, "description_too_long", "知識庫說明最多 2000 個字元。");
        if (await db.Set<WorkspaceResource>().CountAsync(x => x.OwnerId == actor && x.Kind == "knowledge" && !x.IsDeleted, ct) >= options.Value.MaxCollections)
            throw new ApiException(409, "collection_limit", "個人知識庫數量已達上限。");
        var resource = new WorkspaceResource { OwnerId = actor, Kind = "knowledge", Name = name };
        db.Add(resource); db.Add(new KnowledgeCollection { Id = resource.Id, Description = request.Description.Trim() });
        db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = resource.Id, Action = "knowledge.created", Result = "created" });
        await db.SaveChangesAsync(ct);
        return new(await access.DescribeAsync(actor, resource, ct), request.Description.Trim(), 0, 0);
    }
    public async Task<IReadOnlyList<DocumentDto>> ListAsync(Guid actor, Guid collection, CancellationToken ct)
    {
        var resource = await access.RequireAsync(actor, collection, "knowledge", ct);
        var editable = (await access.DescribeAsync(actor, resource, ct)).CanEdit;
        var docs = await db.Set<KnowledgeDocument>().AsNoTracking().Where(x => x.CollectionId == collection && !x.IsDeleted).OrderBy(x => x.FileName).Take(options.Value.MaxDocumentsPerCollection).ToListAsync(ct);
        var ids = docs.Select(x => x.JobId).ToArray();
        var states = await db.Set<BackgroundJob>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Status, ct);
        return docs.Select(x => Describe(x, editable, x.JobId is Guid job && states.TryGetValue(job, out var state) ? state : null)).ToList();
    }
    public async Task UpdateCollectionAsync(Guid actor, Guid id, CollectionRequest request, CancellationToken ct)
    {
        var resource = await access.RequireAsync(actor, id, "knowledge", ct, write: true);
        resource.Name = ResourceAccess.Name(request.Name);
        if (request.Description.Length > 2000) throw new ApiException(400, "description_too_long", "知識庫說明最多 2000 個字元。");
        (await db.Set<KnowledgeCollection>().SingleAsync(x => x.Id == id, ct)).Description = request.Description.Trim(); resource.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "knowledge.updated", Result = "saved" });
        await db.SaveChangesAsync(ct);
    }
    public async Task DeleteCollectionAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await access.OwnerAsync(actor, id, "knowledge", ct); await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await attachments.LockOwnerAsync(actor, ct);
            var resource = await access.OwnerAsync(actor, id, "knowledge", ct);
            var docs = await db.Set<KnowledgeDocument>().Where(x => x.CollectionId == id && !x.IsDeleted).ToListAsync(ct);
            var documentIds = docs.Select(x => x.Id).ToArray(); var files = docs.Where(x => x.AttachmentId != null).Select(x => x.AttachmentId!.Value).Distinct().ToArray();
            foreach (var doc in docs) { doc.IsDeleted = true; doc.AttachmentId = null; doc.Status = "deleted"; }
            resource.IsDeleted = true;
            await db.Set<WorkspaceResource>().Where(x => documentIds.Contains(x.Id)).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true), ct);
            await db.Set<BackgroundJob>().Where(x => documentIds.Contains(x.SubjectId) && (x.Status == "queued" || x.Status == "running")).ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true), ct);
            await db.Set<AttachmentReference>().Where(x => documentIds.Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
            await db.Set<ConversationKnowledge>().Where(x => x.CollectionId == id).ExecuteDeleteAsync(ct);
            await db.Set<KnowledgeChunk>().Where(x => documentIds.Contains(x.DocumentId)).ExecuteDeleteAsync(ct);
            await db.Set<DocumentPage>().Where(x => documentIds.Contains(x.DocumentId)).ExecuteDeleteAsync(ct);
            await db.SaveChangesAsync(ct);
            await lifecycle.MarkUnusedAsync(files, ct);
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "knowledge.deleted", Result = "deleted" }); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
        await lifecycle.DeletePendingAsync(ct);
    }
    public async Task<DocumentDto> ReindexAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            var doc = await RequireAsync(actor, id, ct, write: true);
            if (doc.CollectionId is null) throw new ApiException(409, "document_not_indexed", "只有知識庫文件需要重新索引。");
            if (await db.Set<BackgroundJob>().AnyAsync(x => x.SubjectId == id && (x.Status == "queued" || x.Status == "running"), ct)) throw new ApiException(409, "job_active", "此文件仍在處理中。");
            doc.Status = "queued"; doc.Warning = null; doc.JobId = jobs.Enqueue(actor, doc.Id, doc.Id, "document-ingest", doc.FileName).Id;
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "document.reindexed", Result = "queued" });
            await db.SaveChangesAsync(ct);
            return Describe(doc, true);
        }
        finally { writes.Gate.Release(); }
    }
    public async Task<DocumentDto> AddAsync(Guid actor, Guid? collection, Guid attachment, CancellationToken ct, Guid? project = null)
    {
        if (collection is Guid collectionId) await access.RequireAsync(actor, collectionId, "knowledge", ct, write: true);
        if (project is Guid projectId) await access.RequireAsync(actor, projectId, "project", ct, write: true);
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await attachments.LockOwnerAsync(actor, ct);
            if (collection is Guid currentCollection) await access.RequireAsync(actor, currentCollection, "knowledge", ct, write: true);
            if (project is Guid currentProject) await access.RequireAsync(actor, currentProject, "project", ct, write: true);
            var file = await attachments.OwnedAsync(actor, attachment, ct);
            if (collection is null)
            {
                var existing = await (from doc in db.Set<KnowledgeDocument>() join ownerResource in db.Set<WorkspaceResource>() on doc.Id equals ownerResource.Id where doc.AttachmentId == attachment && doc.CollectionId == null && !doc.IsDeleted && ownerResource.OwnerId == actor && ownerResource.ParentId == project select doc).FirstOrDefaultAsync(ct);
                if (existing is not null) return await DescribeAsync(actor, existing, ct);
            }
            else
            {
                var existing = await db.Set<KnowledgeDocument>().SingleOrDefaultAsync(x => x.CollectionId == collection && x.AttachmentId == attachment && !x.IsDeleted, ct);
                if (existing is not null) return await DescribeAsync(actor, existing, ct);
                if (await db.Set<KnowledgeDocument>().CountAsync(x => x.CollectionId == collection && !x.IsDeleted, ct) >= options.Value.MaxDocumentsPerCollection)
                    throw new ApiException(409, "document_limit", "此知識庫的文件數量已達上限。");
            }
            var resource = new WorkspaceResource { OwnerId = actor, ParentId = project, Kind = "document", Name = string.Concat(file.FileName.Take(120)) };
            var document = new KnowledgeDocument { Id = resource.Id, AttachmentId = file.Id, CollectionId = collection, FileName = file.FileName, ContentType = file.ContentType };
            db.Add(resource); db.Add(document); db.Add(new AttachmentReference { ResourceId = resource.Id, AttachmentId = file.Id });
            if (collection is not null || project is not null)
                await db.Set<Attachment>().Where(x => x.Id == file.Id && x.OwnerId == actor).ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, true), ct);
            // Reuse completed extraction for the same immutable original; each collection keeps its own ACL/index.
            var extracted = await db.Set<KnowledgeDocument>().AsNoTracking().Where(x => x.AttachmentId == attachment && !x.IsDeleted && x.Status == "ready").OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
            if (extracted is not null)
            {
                var pages = await db.Set<DocumentPage>().AsNoTracking().Where(x => x.DocumentId == extracted.Id).ToListAsync(ct);
                foreach (var page in pages) db.Add(new DocumentPage { DocumentId = document.Id, PageNumber = page.PageNumber, Text = page.Text, Extraction = page.Extraction, NeedsReview = page.NeedsReview });
                document.Warning = extracted.Warning;
            }
            document.JobId = jobs.Enqueue(actor, resource.Id, document.Id, "document-ingest", file.FileName).Id;
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = resource.Id, Action = "document.created", Result = "queued" });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return Describe(document, true);
        }
        finally { writes.Gate.Release(); }
    }
    public async Task<KnowledgeDocument> RequireAsync(Guid actor, Guid id, CancellationToken ct, bool write = false)
    {
        var doc = await db.Set<KnowledgeDocument>().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw Missing();
        if (doc.CollectionId is Guid collection)
        {
            if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "knowledge")) throw Missing();
            await access.RequireAsync(actor, collection, "knowledge", ct, write);
        }
        else
        {
            if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id is "chat" or "knowledge" or "projects")) throw Missing();
            var resource = await access.RequireAsync(actor, doc.Id, "document", ct, write);
            if (resource.ParentId is Guid parent) {
                if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "projects")) throw Missing();
                await access.RequireAsync(actor, parent, "project", ct, write);
            }
        }
        return doc;
    }
    public async Task<DocumentDto> DetailAsync(Guid actor, Guid id, CancellationToken ct) => await DescribeAsync(actor, await RequireAsync(actor, id, ct), ct);
    public async Task<DocumentJobDto> JobAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var document = await RequireAsync(actor, id, ct);
        var job = await db.Set<BackgroundJob>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == document.JobId, ct) ?? throw Missing();
        var info = await DescribeAsync(actor, document, ct);
        return new(JobService.Describe(job), job.OwnerId == actor && info.CanEdit);
    }
    public async Task<IReadOnlyList<DocumentPageDto>> PagesAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await RequireAsync(actor, id, ct);
        return await db.Set<DocumentPage>().AsNoTracking().Where(x => x.DocumentId == id).OrderBy(x => x.PageNumber).Select(x => new DocumentPageDto(x.PageNumber, x.Text, x.Extraction, x.NeedsReview)).ToListAsync(ct);
    }
    public async Task<Attachment> OriginalAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var document = await RequireAsync(actor, id, ct);
        if (document.AttachmentId is not Guid attachment) throw Missing();
        return await db.Set<Attachment>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachment && x.StorageState == AttachmentStates.Ready, ct) ?? throw Missing();
    }
    public async Task DeleteAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await RequireAsync(actor, id, ct, write: true);
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await attachments.LockOwnerAsync(actor, ct);
            var document = await RequireAsync(actor, id, ct, write: true);
            document.IsDeleted = true; document.Status = "deleted"; var attachment = document.AttachmentId; document.AttachmentId = null;
            (await db.Set<WorkspaceResource>().SingleAsync(x => x.Id == id, ct)).IsDeleted = true;
            await db.Set<BackgroundJob>().Where(x => x.SubjectId == id && (x.Status == "running" || x.Status == "queued")).ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true), ct);
            await db.Set<AttachmentReference>().Where(x => x.ResourceId == id).ExecuteDeleteAsync(ct);
            await db.Set<KnowledgeChunk>().Where(x => x.DocumentId == id).ExecuteDeleteAsync(ct);
            await db.Set<DocumentPage>().Where(x => x.DocumentId == id).ExecuteDeleteAsync(ct);
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "document.deleted", Result = "deleted" });
            await db.SaveChangesAsync(ct);
            if (attachment is Guid file) await lifecycle.MarkUnusedAsync([file], ct);
            await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
        await lifecycle.DeletePendingAsync(ct);
    }
    private async Task<DocumentDto> DescribeAsync(Guid actor, KnowledgeDocument doc, CancellationToken ct)
    {
        var resource = doc.CollectionId is Guid collection ? await access.RequireAsync(actor, collection, "knowledge", ct) : await access.RequireAsync(actor, doc.Id, "document", ct);
        var editable = (await access.DescribeAsync(actor, resource, ct)).CanEdit;
        var state = doc.JobId is Guid job ? await db.Set<BackgroundJob>().Where(x => x.Id == job).Select(x => x.Status).SingleOrDefaultAsync(ct) : null;
        return Describe(doc, editable, state);
    }
    private static DocumentDto Describe(KnowledgeDocument x, bool edit, string? jobStatus = null) => new(x.Id, x.CollectionId, x.FileName, x.ContentType, x.Status == "ready" ? "ready" : jobStatus ?? x.Status, x.PageCount, x.ChunkCount, x.Warning, x.JobId, edit, x.AttachmentId != null);
    private static ApiException Missing() => new(404, "document_not_found", "找不到此文件，或你已沒有存取權限。");
}
