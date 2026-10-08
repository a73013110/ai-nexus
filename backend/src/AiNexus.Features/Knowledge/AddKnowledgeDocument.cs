using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Operations;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Knowledge;

// Every check needs the database (collection access first, then the caller's own attachment), and the project files
// endpoint takes the same body, so this request has no validator.
public sealed record AddDocumentRequest(Guid AttachmentId);

/// <summary>
/// Adds one of the caller's attachments as a document of a collection they may edit, of a project (through
/// <see cref="DocumentService"/>), or as a standalone document, and queues its ingest job. Adding the same attachment
/// again returns the existing document. Extraction already completed for the same original is copied, never redone.
/// </summary>
internal sealed class AddKnowledgeDocument(NexusDbContext db, ResourceAccess access, AttachmentService attachments, AttachmentWriteLock writes, JobService jobs,
    DocumentAccess documents, IOptions<KnowledgeOptions> options, TimeProvider clock)
{
    public static RouteHandlerBuilder MapCollection(RouteGroupBuilder routes) => routes
        .MapPost("/collections/{id:guid}/documents", async (Guid id, AddDocumentRequest request, ICurrentUser user, AddKnowledgeDocument handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, request.AttachmentId, ct)).ToHttpResult())
        .WithName("AddKnowledgeDocument").Produces<DocumentDto>();

    /// <summary>Reads an attachment as a standalone document, for the conversation document viewer.</summary>
    public static RouteHandlerBuilder MapAttachment(RouteGroupBuilder api) => api
        .MapPost("/attachments/{id:guid}/document", async (Guid id, ICurrentUser user, AddKnowledgeDocument handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, null, id, ct)).ToHttpResult())
        .RequireAuthorization(Policies.Attachments).WithName("ReadAttachmentDocument").Produces<DocumentDto>();

    public async Task<Result<DocumentDto>> HandleAsync(Guid actor, Guid? collection, Guid attachment, CancellationToken ct, Guid? project = null, TextDocumentRequest? text = null)
    {
        if (collection is Guid collectionId) await access.RequireAsync(actor, collectionId, KnowledgeCollection.Kind, ct, write: true);
        if (project is Guid projectId) await access.RequireAsync(actor, projectId, "project", ct, write: true);
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await attachments.LockOwnerAsync(actor, ct);
            if (collection is Guid currentCollection) await access.RequireAsync(actor, currentCollection, KnowledgeCollection.Kind, ct, write: true);
            if (project is Guid currentProject) await access.RequireAsync(actor, currentProject, "project", ct, write: true);
            var file = await attachments.OwnedAsync(actor, attachment, ct);
            if (collection is null)
            {
                var existing = await (from doc in db.Set<KnowledgeDocument>() join ownerResource in db.Set<WorkspaceResource>() on doc.Id equals ownerResource.Id where doc.AttachmentId == attachment && doc.CollectionId == null && !doc.IsDeleted && ownerResource.OwnerId == actor && ownerResource.ParentId == project select doc).FirstOrDefaultAsync(ct);
                if (existing is not null) return await documents.DescribeAsync(actor, existing, ct);
            }
            else
            {
                var existing = await db.Set<KnowledgeDocument>().SingleOrDefaultAsync(x => x.CollectionId == collection && x.AttachmentId == attachment && !x.IsDeleted, ct);
                if (existing is not null) return await documents.DescribeAsync(actor, existing, ct);
                if (await db.Set<KnowledgeDocument>().CountAsync(x => x.CollectionId == collection && !x.IsDeleted, ct) >= options.Value.MaxDocumentsPerCollection)
                    return KnowledgeErrors.DocumentLimit;
            }
            var now = clock.GetUtcNow();
            var resource = new WorkspaceResource { OwnerId = actor, ParentId = project, Kind = KnowledgeDocument.Kind, Name = string.Concat(file.FileName.Take(120)), CreatedAt = now, UpdatedAt = now };
            var document = new KnowledgeDocument { Id = resource.Id, AttachmentId = file.Id, CollectionId = collection, FileName = file.FileName, ContentType = file.ContentType };
            if (text is not null) { document.TextContent = text.Text; document.TextVersion = 1; }
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
            return DocumentAccess.Describe(document, true);
        }
        finally { writes.Gate.Release(); }
    }
}
