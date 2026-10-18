using AiNexus.Data.App;
using AiNexus.Data.App.Attachments;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Knowledge;
using AiNexus.Platform.Validation;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Knowledge.Documents;

[ValidatedInHandler("Every check needs the database (collection access first, then the caller's own attachment); the project files endpoint shares the body.")]
public sealed record AddDocumentRequest(Guid AttachmentId);

/// <summary>
/// 把呼叫者的附件加入知識庫（需有編輯權）、專案（經 <see cref="DocumentService"/>）或成為獨立文件，並排入匯入工作。
/// 同一附件重複加入會回傳既有文件；同一原始檔若已完成擷取，直接複製擷取結果，不重跑擷取。
/// </summary>
internal sealed class AddKnowledgeDocument(AppDbContext db, ResourceAccess access, AttachmentService attachments, AttachmentWriteLock writes, JobService jobs,
    DocumentAccess documents, IOptions<KnowledgeOptions> options, TimeProvider clock)
{
    public static RouteHandlerBuilder MapCollection(RouteGroupBuilder routes) => routes
        .MapPost("/collections/{id:guid}/documents", async (Guid id, AddDocumentRequest request, ICurrentUser user, AddKnowledgeDocument handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, request.AttachmentId, ct)).ToHttpResult())
        .WithName("AddKnowledgeDocument");

    /// <summary>把附件讀成獨立文件，供對話中的文件檢視器使用。</summary>
    public static RouteHandlerBuilder MapAttachment(RouteGroupBuilder api) => api
        .MapPost("/attachments/{id:guid}/document", async (Guid id, ICurrentUser user, AddKnowledgeDocument handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, null, id, ct)).ToHttpResult())
        .RequireAuthorization(Policies.Attachments).WithName("ReadAttachmentDocument");

    public async Task<Result<DocumentDto>> HandleAsync(Guid actor, Guid? collection, Guid attachment, CancellationToken ct, Guid? project = null, TextDocumentRequest? text = null)
    {
        if (await RequireContainerAsync(actor, collection, project, ct) is { IsSuccess: false } denied) return denied.Error;
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await attachments.LockOwnerAsync(actor, ct);
            if (await RequireContainerAsync(actor, collection, project, ct) is { IsSuccess: false } revoked) return revoked.Error;
            var owned = await attachments.OwnedAsync(actor, attachment, ct);
            if (!owned.IsSuccess) return owned.Error;
            var file = owned.Value;
            if (collection is null)
            {
                var existing = await (from doc in db.Set<Document>() join ownerResource in db.Set<Resource>() on doc.Id equals ownerResource.Id where doc.AttachmentId == attachment && doc.CollectionId == null && !doc.IsDeleted && ownerResource.OwnerId == actor && ownerResource.ParentId == project select doc).FirstOrDefaultAsync(ct);
                if (existing is not null) return await documents.DescribeAsync(actor, existing, ct);
            }
            else
            {
                var existing = await db.Set<Document>().SingleOrDefaultAsync(x => x.CollectionId == collection && x.AttachmentId == attachment && !x.IsDeleted, ct);
                if (existing is not null) return await documents.DescribeAsync(actor, existing, ct);
                if (await db.Set<Document>().CountAsync(x => x.CollectionId == collection && !x.IsDeleted, ct) >= options.Value.Indexing.MaxDocumentsPerCollection)
                    return KnowledgeErrors.DocumentLimit;
            }
            var now = clock.GetUtcNow();
            var resource = new Resource { OwnerId = actor, ParentId = project, Kind = Document.Kind, Name = string.Concat(file.FileName.Take(120)), CreatedAt = now, UpdatedAt = now };
            var document = new Document { Id = resource.Id, AttachmentId = file.Id, CollectionId = collection, FileName = file.FileName, ContentType = file.ContentType };
            if (text is not null) { document.TextContent = text.Text; document.TextVersion = 1; }
            db.Add(resource); db.Add(document); db.Add(new ResourceAttachment { ResourceId = resource.Id, AttachmentId = file.Id });
            if (collection is not null || project is not null)
                await db.Set<Attachment>().Where(x => x.Id == file.Id && x.OwnerId == actor).ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, true), ct);
            // 同一原始檔不會改變，已完成的擷取可直接複製；但每個知識庫仍各自保有 ACL 與索引。
            var extracted = await db.Set<Document>().AsNoTracking().Where(x => x.AttachmentId == attachment && !x.IsDeleted && x.Status == "ready").OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
            if (extracted is not null)
            {
                var pages = await db.Set<DocumentPage>().AsNoTracking().Where(x => x.DocumentId == extracted.Id).ToListAsync(ct);
                foreach (var page in pages) db.Add(new DocumentPage { DocumentId = document.Id, PageNumber = page.PageNumber, Text = page.Text, Extraction = page.Extraction, NeedsReview = page.NeedsReview });
                document.Warning = extracted.Warning;
            }
            document.JobId = jobs.Enqueue(actor, resource.Id, document.Id, "document-ingest", file.FileName).Id;
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = resource.Id, Action = "document.created", Result = "queued" });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return DocumentAccess.Describe(document, true);
        }
        finally { writes.Gate.Release(); }
    }

    private async Task<Result> RequireContainerAsync(Guid actor, Guid? collection, Guid? project, CancellationToken ct)
    {
        if (collection is Guid collectionId && await access.RequireAsync(actor, collectionId, Collection.Kind, ct, write: true) is { IsSuccess: false } noCollection) return noCollection.Error;
        if (project is Guid projectId && await access.RequireAsync(actor, projectId, "project", ct, write: true) is { IsSuccess: false } noProject) return noProject.Error;
        return Result.Success;
    }
}
