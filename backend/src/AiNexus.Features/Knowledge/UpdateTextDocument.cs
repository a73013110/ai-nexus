using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Knowledge;

/// <summary>
/// Replaces the text of a plain-text source the user may edit and reindexes it. The edit names the version it was based
/// on; a newer saved version, or a source still being processed, is a conflict.
/// </summary>
internal sealed class UpdateTextDocument(NexusDbContext db, DocumentAccess documents, AttachmentService attachments, AttachmentWriteLock writes, AttachmentQuota quota, JobService jobs, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}/text", async (Guid id, TextDocumentRequest body, ICurrentUser user, UpdateTextDocument handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .WithRequestBodyLimit(RequestBodyLimits.ForJsonCharacters(TextDocuments.MaxCharacters)).WithName("UpdateTextDocument").Produces<DocumentDto>();

    public async Task<Result<DocumentDto>> HandleAsync(Guid actor, Guid id, TextDocumentRequest request, CancellationToken ct)
    {
        var original = await documents.FindAsync(actor, id, ct, write: true);
        if (!original.IsSuccess) return original.Error;
        if (original.Value.TextContent is null) return KnowledgeErrors.NotEditableText;
        if (request.ExpectedVersion != original.Value.TextVersion) return KnowledgeErrors.TextVersionChanged;
        var cleaned = TextDocuments.Clean(request);
        if (!cleaned.IsSuccess) return cleaned.Error;
        var source = cleaned.Value;
        var uploaded = await TextDocuments.UploadAsync(attachments, actor, source, ct);
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await quota.LockOwnerAsync(actor, ct);
            var current = await documents.FindAsync(actor, id, ct, write: true);
            if (!current.IsSuccess) return current.Error;
            var document = current.Value;
            await db.Entry(document).ReloadAsync(ct);
            if (document.IsDeleted || document.TextContent is null || request.ExpectedVersion != document.TextVersion) return KnowledgeErrors.TextVersionChanged;
            if (await db.Set<BackgroundJob>().AnyAsync(x => x.SubjectId == id && x.ActiveKey != null, ct)) return KnowledgeErrors.DocumentProcessing;
            document.TextContent = source.Text; document.TextVersion++; document.FileName = source.Title + ".txt";
            document.AttachmentId = uploaded.Id; document.Status = "queued"; document.PageCount = 0; document.ChunkCount = 0;
            document.Warning = null;
            await db.Set<AttachmentReference>().Where(x => x.ResourceId == id).ExecuteDeleteAsync(ct);
            db.Add(new AttachmentReference { ResourceId = id, AttachmentId = uploaded.Id });
            await db.Set<Attachment>().Where(x => x.Id == uploaded.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, true), ct);
            await db.Set<DocumentPage>().Where(x => x.DocumentId == id).ExecuteDeleteAsync(ct);
            var resource = await db.Set<WorkspaceResource>().IgnoreQueryFilters([SoftDelete.Filter]).SingleAsync(x => x.Id == id, ct);
            resource.Name = source.Title; resource.UpdatedAt = clock.GetUtcNow();
            document.JobId = jobs.Enqueue(actor, id, id, "document-ingest", document.FileName).Id;
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "document.text.updated", Result = "reindexing" });
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return KnowledgeErrors.TextVersionChanged; }
            await transaction.CommitAsync(ct);
            return await documents.DetailAsync(actor, id, ct);
        }
        finally { writes.Gate.Release(); }
    }
}
