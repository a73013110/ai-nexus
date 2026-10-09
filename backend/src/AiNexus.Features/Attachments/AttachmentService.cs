using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Attachments;

/// <summary>
/// The module's contract for other modules: upload, ownership lookup, quota locking and reading stored bytes. These
/// methods report failures as <see cref="ApiException"/> because their callers do; the module's own endpoints use
/// <see cref="FindOwnedAsync"/> and their slices.
/// </summary>
public sealed class AttachmentService(DocumentExtractor extractor, IOptions<AttachmentOptions> options, AttachmentWriteLock writes, NexusDbContext db, IAttachmentStorage storage, AttachmentQuota quota, AttachmentLifecycle lifecycle, TimeProvider clock)
{
    public static AttachmentDto Describe(Attachment file) => new(file.Id, file.FileName, file.ContentType, file.Size, file.ContentType.StartsWith("image/", StringComparison.Ordinal), file.ContentType == "application/pdf" && string.IsNullOrWhiteSpace(file.ExtractedText) ? "ocr-required" : file.ExtractedText is null ? "vision" : "extracted-text");

    public async Task<AttachmentDto> UploadAsync(Guid owner, IFormFile file, CancellationToken ct)
    {
        if (file.Length is < 1 || file.Length > options.Value.MaxFileBytes) throw new ApiException(413, "file_size_limit", $"每個檔案最多 {options.Value.MaxFileBytes / 1024 / 1024} MB。");
        var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (name.Length is < 1 or > 180 || name.Any(char.IsControl)) throw new ApiException(400, "invalid_file_name", "檔案名稱不正確或過長。");
        using var data = new MemoryStream();
        await using (var input = file.OpenReadStream())
        {
            var buffer = new byte[65536];
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (data.Length + read > options.Value.MaxFileBytes) throw new ApiException(413, "file_size_limit", "檔案過大。");
                await data.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        if (data.Length == 0 || data.Length != file.Length) throw new ApiException(400, "file_size_mismatch", "上傳檔案內容不完整，請重新上傳。");
        var bytes = data.ToArray();
        var (type, text) = extractor.Extract(name, bytes, ct);
        // Only this owner's expired drafts: they must not count against the quota checked below. Everyone else's are the cleanup worker's.
        await lifecycle.ReclaimAsync(ct, owner);
        var attachment = new Attachment { OwnerId = owner, FileName = name, ContentType = type, Size = bytes.Length, ExtractedText = text, StorageState = AttachmentStates.Pending, CreatedAt = clock.GetUtcNow() };
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await quota.ReserveAsync(owner, bytes.Length, ct);
            db.Set<Attachment>().Add(attachment);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
        try
        {
            await storage.WriteAsync(attachment.StorageKey, bytes, ct);
            var activated = await db.Set<Attachment>().Where(x => x.Id == attachment.Id && x.StorageState == AttachmentStates.Pending)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.StorageState, AttachmentStates.Ready), ct);
            if (activated != 1) throw new ApiException(409, "attachment_upload_expired", "上傳已失效，請重新上傳。");
            return Describe(attachment);
        }
        catch
        {
            await lifecycle.AbortUploadAsync(attachment, CancellationToken.None);
            throw;
        }
    }

    public Task<int> LockOwnerAsync(Guid owner, CancellationToken ct) => quota.LockOwnerAsync(owner, ct);
    public async Task<Stream> OpenAsync(Attachment file, CancellationToken ct)
    {
        var stream = await storage.OpenReadAsync(file.StorageKey, ct);
        if (stream.CanSeek && stream.Length != file.Size)
        {
            await stream.DisposeAsync();
            throw new ApiException(503, "attachment_content_invalid", "附件原檔與儲存記錄不符，請聯絡管理員檢查備份。");
        }
        return stream;
    }
    public async Task<byte[]> ReadAsync(Attachment file, CancellationToken ct)
    {
        await using var stream = await OpenAsync(file, ct);
        using var data = new MemoryStream();
        await stream.CopyToAsync(data, ct);
        return data.ToArray();
    }

    public async Task<Attachment> OwnedAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var file = await FindOwnedAsync(owner, id, ct);
        return file.IsSuccess ? file.Value : throw new ApiException(404, file.Error.Code, "找不到這個附件。");
    }

    /// <summary>
    /// A ready attachment of <paramref name="owner"/>. A file sent only in conversations stays reachable while one of
    /// the owner's conversations that holds it still exists.
    /// </summary>
    internal async Task<Result<Attachment>> FindOwnedAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var file = await db.Set<Attachment>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner && x.StorageState == AttachmentStates.Ready, ct);
        if (file is null) return AttachmentsErrors.NotFound;
        if (!file.InLibrary && !await db.Set<AttachmentReference>().AnyAsync(x => x.AttachmentId == id, ct) && await db.Set<MessageAttachment>().AnyAsync(x => x.AttachmentId == id, ct) &&
            !await (from link in db.Set<MessageAttachment>() join message in db.Set<Message>() on link.MessageId equals message.Id join conversation in db.Set<Conversation>() on message.ConversationId equals conversation.Id where link.AttachmentId == id && conversation.OwnerId == owner select link).AnyAsync(ct))
            return AttachmentsErrors.NotFound;
        return file;
    }

    public async Task<IReadOnlyList<Attachment>> RequireAsync(Guid owner, IReadOnlyList<Guid>? ids, CancellationToken ct)
    {
        if (ids is null || ids.Count == 0) return [];
        if (ids.Count > options.Value.MaxFilesPerMessage || ids.Distinct().Count() != ids.Count) throw new ApiException(400, "attachment_limit", $"每則提問最多 {options.Value.MaxFilesPerMessage} 個不同附件。");
        var files = new List<Attachment>();
        foreach (var id in ids) files.Add(await OwnedAsync(owner, id, ct));
        if (files.Any(x => x.ContentType == "application/pdf" && string.IsNullOrWhiteSpace(x.ExtractedText))) throw new ApiException(409, "document_processing_required", "掃描 PDF 尚未完成文字辨識，請等待附件處理完成後再送出。");
        if (files.Sum(x => x.Size) > options.Value.MaxMessageBytes) throw new ApiException(413, "attachment_total_limit", "這次附件總大小超過上限。");
        return files;
    }
}
