using System.Text;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Collaboration;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Knowledge;

public sealed class TextDocumentService(NexusDbContext db, DocumentService documents, ResourceAccess access,
    AttachmentService attachments, AttachmentWriteLock writes, AttachmentQuota quota, JobService jobs)
{
    public const int MaxCharacters = 64000;
    public async Task<TextDocumentDto> ReadAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var document = await documents.RequireAsync(actor, id, ct);
        if (document.TextContent is null) throw new ApiException(409, "document_not_editable_text", "只有以純文字建立的來源可以編輯。");
        return new(id, Path.GetFileNameWithoutExtension(document.FileName), document.TextContent, document.TextVersion);
    }
    public async Task<DocumentDto> CreateAsync(Guid actor, Guid collection, TextDocumentRequest request, CancellationToken ct)
    {
        await access.RequireAsync(actor, collection, "knowledge", ct, write: true);
        var source = Clean(request);
        var file = await UploadAsync(actor, source, ct);
        return await documents.AddAsync(actor, collection, file.Id, ct, text: source);
    }
    public async Task<DocumentDto> UpdateAsync(Guid actor, Guid id, TextDocumentRequest request, CancellationToken ct)
    {
        var original = await documents.RequireAsync(actor, id, ct, write: true);
        if (original.TextContent is null) throw new ApiException(409, "document_not_editable_text", "只有以純文字建立的來源可以編輯。");
        if (request.ExpectedVersion != original.TextVersion) throw Conflict();
        var source = Clean(request);
        var uploaded = await UploadAsync(actor, source, ct);
        await writes.Gate.WaitAsync(ct);
        try {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await quota.LockOwnerAsync(actor, ct);
            var document = await documents.RequireAsync(actor, id, ct, write: true);
            await db.Entry(document).ReloadAsync(ct);
            if (document.IsDeleted || document.TextContent is null || request.ExpectedVersion != document.TextVersion) throw Conflict();
            if (await db.Set<BackgroundJob>().AnyAsync(x => x.SubjectId == id && x.ActiveKey != null, ct))
                throw new ApiException(409, "document_processing", "來源仍在處理，請等待完成或停止處理後再編輯。");
            document.TextContent = source.Text; document.TextVersion++; document.FileName = source.Title + ".txt";
            document.AttachmentId = uploaded.Id; document.Status = "queued"; document.PageCount = 0; document.ChunkCount = 0;
            document.EmbeddingProfile = null; document.Warning = null;
            await db.Set<AttachmentReference>().Where(x => x.ResourceId == id).ExecuteDeleteAsync(ct);
            db.Add(new AttachmentReference { ResourceId = id, AttachmentId = uploaded.Id });
            await db.Set<Attachment>().Where(x => x.Id == uploaded.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, true), ct);
            await db.Set<KnowledgeChunk>().Where(x => x.DocumentId == id).ExecuteDeleteAsync(ct);
            await db.Set<DocumentPage>().Where(x => x.DocumentId == id).ExecuteDeleteAsync(ct);
            var resource = await db.Set<WorkspaceResource>().SingleAsync(x => x.Id == id, ct);
            resource.Name = source.Title; resource.UpdatedAt = DateTimeOffset.UtcNow;
            document.JobId = jobs.Enqueue(actor, id, id, "document-ingest", document.FileName).Id;
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "document.text.updated", Result = "reindexing" });
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw Conflict(); }
            await transaction.CommitAsync(ct);
            return await documents.DetailAsync(actor, id, ct);
        } finally { writes.Gate.Release(); }
    }
    private async Task<AttachmentDto> UploadAsync(Guid actor, TextDocumentRequest request, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(request.Text);
        using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", request.Title + ".txt") { Headers = new HeaderDictionary(), ContentType = "text/plain" };
        return await attachments.UploadAsync(actor, file, ct);
    }
    private static TextDocumentRequest Clean(TextDocumentRequest request)
    {
        var title = ResourceAccess.Name(request.Title);
        if (title.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || title.Any(char.IsControl))
            throw new ApiException(400, "text_title_invalid", "來源名稱不可包含路徑或特殊符號。");
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > MaxCharacters || request.Text.Contains('\0'))
            throw new ApiException(400, "text_content_invalid", $"請提供 1 至 {MaxCharacters:N0} 個字元的純文字內容。");
        return request with { Title = title };
    }
    private static ApiException Conflict() => new(409, "text_version_changed", "來源已被其他人修改。請重新開啟編輯器，再合併你的內容。");
}
