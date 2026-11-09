using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Knowledge;
using AiNexus.Data.App.Projects;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Validation;
using AiNexus.Features.Work.Files;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Knowledge.Indexing;
using AiNexus.Platform.Strings;

namespace AiNexus.Features.Work.Knowledge.Documents;

[ValidatedInHandler("Every check needs the database (collection access first, then the caller's own attachment); the project files endpoint shares the body.")]
public sealed record AddDocumentRequest(Guid AttachmentId);

/// <summary>
/// 把呼叫者的附件加入知識庫（需有編輯權）、專案（經 <see cref="DocumentService"/>）或成為獨立文件，並排入匯入工作。
/// 同一附件重複加入會回傳既有文件；同一原始檔若已完成擷取，直接複製擷取結果，不重跑擷取。
/// </summary>
internal sealed class AddKnowledgeDocument(AppDbContext db, ResourceAccess access, AttachmentService attachments, DocumentWriteLock writes, ContainerLock containers, JobService jobs,
    DocumentAccess documents, IOptions<KnowledgeOptions> options, TimeProvider clock, AuditTrail audit) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/collections/{id:guid}/documents", (Guid id, AddDocumentRequest request, ICurrentUser user, AddKnowledgeDocument handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, request.AttachmentId, ct))
        .Audited(KnowledgeAudit.DocumentCreated)
        .WithName("AddKnowledgeDocument");

    public async Task<Result<DocumentDto>> HandleAsync(Guid actor, Guid? collection, Guid attachment, CancellationToken ct, Guid? project = null, TextDocumentRequest? text = null, Action<Document>? alongside = null)
    {
        if (await RequireContainerAsync(actor, collection, project, ct) is { IsSuccess: false } denied) return denied.Error;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (project is Guid container && !await containers.TryStartWorkAsync(container, ct)) return CollaborationErrors.ResourceBusy;
        // 不同人可以往同一個知識庫加文件：擁有者列鎖擋不住他們，文件上限與刪除知識庫靠這把鎖。
        if (collection is Guid target && !await writes.TryAcquireForCollectionAsync(target, ct)) return CollaborationErrors.ResourceBusy;
        await attachments.LockOwnerAsync(actor, ct);
        if (await RequireContainerAsync(actor, collection, project, ct) is { IsSuccess: false } revoked) return revoked.Error;
        var owned = await attachments.OwnedAsync(actor, attachment, ct);
        if (!owned.IsSuccess) return owned.Error;
        var file = owned.Value;
        if (collection is null)
        {
            var existing = await (from doc in db.Documents join ownerResource in db.Resources on doc.Id equals ownerResource.Id where doc.AttachmentId == attachment && doc.CollectionId == null && !doc.IsDeleted && ownerResource.OwnerId == actor && ownerResource.ParentId == project select doc).FirstOrDefaultAsync(ct);
            if (existing is not null) return await documents.DescribeAsync(actor, existing, ct);
        }
        else
        {
            var existing = await db.Documents.SingleOrDefaultAsync(x => x.CollectionId == collection && x.AttachmentId == attachment && !x.IsDeleted, ct);
            if (existing is not null) return await documents.DescribeAsync(actor, existing, ct);
            if (await db.Documents.CountAsync(x => x.CollectionId == collection && !x.IsDeleted, ct) >= options.Value.Indexing.MaxDocumentsPerCollection)
                return KnowledgeErrors.DocumentLimit;
        }
        var now = clock.GetUtcNow();
        var resource = new Resource { OwnerId = actor, ParentId = project, Kind = Document.Kind, Name = file.FileName.Clip(120), CreatedAt = now, UpdatedAt = now };
        var document = new Document { Id = resource.Id, AttachmentId = file.Id, CollectionId = collection, FileName = file.FileName, ContentType = file.ContentType };
        if (text is not null) { document.TextContent = text.Text; document.TextVersion = 1; }
        db.Add(resource); db.Add(document); db.Add(new ResourceAttachment { ResourceId = resource.Id, AttachmentId = file.Id });
        if (collection is not null || project is not null)
            await db.Attachments.Where(x => x.Id == file.Id && x.OwnerId == actor).ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, true), ct);
        // 同一原始檔不會改變，已完成的擷取可直接複製；但每個知識庫仍各自保有 ACL 與索引。
        var extracted = await db.Documents.AsNoTracking().Where(x => x.AttachmentId == attachment && !x.IsDeleted && x.Status == "ready").OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (extracted is not null)
        {
            var pages = await db.DocumentPages.AsNoTracking().Where(x => x.DocumentId == extracted.Id).ToListAsync(ct);
            foreach (var page in pages) db.Add(new DocumentPage { DocumentId = document.Id, PageNumber = page.PageNumber, Text = page.Text, Extraction = page.Extraction, NeedsReview = page.NeedsReview });
            document.Warning = extracted.Warning;
        }
        document.JobId = jobs.Enqueue(actor, resource.Id, document.Id, DocumentIngestHandler.JobKind, file.FileName).Id;
        alongside?.Invoke(document);
        audit.Describe(resource: resource.Id);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return DocumentAccess.Describe(document, true);
    }

    private async Task<Result> RequireContainerAsync(Guid actor, Guid? collection, Guid? project, CancellationToken ct)
    {
        if (collection is Guid collectionId && await access.RequireAsync(actor, collectionId, Collection.Kind, ct, write: true) is { IsSuccess: false } noCollection) return noCollection.Error;
        if (project is Guid projectId && await access.RequireAsync(actor, projectId, Project.Kind, ct, write: true) is { IsSuccess: false } noProject) return noProject.Error;
        return Result.Success;
    }
}
