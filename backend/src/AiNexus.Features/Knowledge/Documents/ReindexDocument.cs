using AiNexus.Features.Attachments;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>
/// 把使用者可編輯的知識庫文件重新排入索引：頁面已擷取過就只重做嵌入，否則完整重跑匯入。仍在處理中的文件回傳衝突。
/// </summary>
internal sealed class ReindexDocument(NexusDbContext db, DocumentAccess documents, AttachmentWriteLock writes, JobService jobs)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/reindex", async (Guid id, ICurrentUser user, ReindexDocument handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("ReindexDocument");

    public async Task<Result<DocumentDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            var found = await documents.FindAsync(actor, id, ct, write: true);
            if (!found.IsSuccess) return found.Error;
            var doc = found.Value;
            if (doc.CollectionId is null) return KnowledgeErrors.DocumentNotIndexed;
            if (await db.Set<BackgroundJob>().AnyAsync(x => x.SubjectId == id && (x.Status == "queued" || x.Status == "running"), ct)) return KnowledgeErrors.JobActive;
            var savedPages = await db.Set<DocumentPage>().AnyAsync(x => x.DocumentId == id, ct);
            doc.Status = "queued"; doc.JobId = jobs.Enqueue(actor, doc.Id, doc.Id, savedPages ? "document-embedding" : "document-ingest", doc.FileName).Id;
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "document.reindexed", Result = "queued" });
            await db.SaveChangesAsync(ct);
            return DocumentAccess.Describe(doc, true);
        }
        finally { writes.Gate.Release(); }
    }
}
