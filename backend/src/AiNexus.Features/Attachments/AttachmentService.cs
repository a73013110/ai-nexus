using AiNexus.Features.Attachments.Extraction;
using AiNexus.Features.Attachments.Storage;
using AiNexus.Features.Conversations;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Attachments;

/// <summary>
/// 附件模組對其他模組的契約：上傳、擁有者查詢、配額鎖定與讀取儲存位元組。
/// 儲存的檔案若遺失或與記錄不符，屬於基礎設施錯誤，會直接拋出例外。
/// </summary>
public sealed class AttachmentService(DocumentExtractor extractor, IOptions<AttachmentOptions> options, AttachmentWriteLock writes, NexusDbContext db, IAttachmentStorage storage, AttachmentQuota quota, AttachmentLifecycle lifecycle, TimeProvider clock)
{
    public static AttachmentDto Describe(Attachment file) => new(file.Id, file.FileName, file.ContentType, file.Size, file.ContentType.StartsWith("image/", StringComparison.Ordinal), file.ContentType == "application/pdf" && string.IsNullOrWhiteSpace(file.ExtractedText) ? "ocr-required" : file.ExtractedText is null ? "vision" : "extracted-text");

    public async Task<Result<AttachmentDto>> UploadAsync(Guid owner, IFormFile file, CancellationToken ct)
    {
        if (file.Length is < 1 || file.Length > options.Value.MaxFileBytes) return AttachmentsErrors.FileSizeLimit;
        var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (name.Length is < 1 or > 180 || name.Any(char.IsControl)) return AttachmentsErrors.InvalidFileName;
        using var data = new MemoryStream();
        await using (var input = file.OpenReadStream())
        {
            var buffer = new byte[65536];
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (data.Length + read > options.Value.MaxFileBytes) return AttachmentsErrors.FileSizeLimit;
                await data.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        if (data.Length == 0 || data.Length != file.Length) return AttachmentsErrors.FileSizeMismatch;
        var bytes = data.ToArray();
        var extracted = extractor.Extract(name, bytes, ct);
        if (!extracted.IsSuccess) return extracted.Error;
        var (type, text) = extracted.Value;
        // 只回收此擁有者的過期草稿，它們不應計入下方的配額檢查；其他人的草稿交給清理 Worker。
        await lifecycle.ReclaimAsync(ct, owner);
        var attachment = new Attachment { OwnerId = owner, FileName = name, ContentType = type, Size = bytes.Length, ExtractedText = text, StorageState = AttachmentStates.Pending, CreatedAt = clock.GetUtcNow() };
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var reserved = await quota.ReserveAsync(owner, bytes.Length, ct);
            if (!reserved.IsSuccess) return reserved.Error;
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
            if (activated == 1) return Describe(attachment);
            await lifecycle.AbortUploadAsync(attachment, CancellationToken.None);
            return AttachmentsErrors.UploadExpired;
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
            throw new ExternalServiceException(AttachmentsErrors.ContentInvalid, "附件原檔與儲存記錄不符，請聯絡管理員檢查備份。");
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

    /// <summary>
    /// 擁有者的就緒附件。只在對話中送出的檔案，只要擁有者仍有持有它的對話，就仍可存取。
    /// </summary>
    public async Task<Result<Attachment>> OwnedAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var file = await db.Set<Attachment>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner && x.StorageState == AttachmentStates.Ready, ct);
        if (file is null) return AttachmentsErrors.NotFound;
        if (!file.InLibrary && !await db.Set<AttachmentReference>().AnyAsync(x => x.AttachmentId == id, ct) && await db.Set<MessageAttachment>().AnyAsync(x => x.AttachmentId == id, ct) &&
            !await (from link in db.Set<MessageAttachment>() join message in db.Set<Message>() on link.MessageId equals message.Id join conversation in db.Set<Conversation>() on message.ConversationId equals conversation.Id where link.AttachmentId == id && conversation.OwnerId == owner select link).AnyAsync(ct))
            return AttachmentsErrors.NotFound;
        return file;
    }

    public async Task<Result<IReadOnlyList<Attachment>>> RequireAsync(Guid owner, IReadOnlyList<Guid>? ids, CancellationToken ct)
    {
        if (ids is null || ids.Count == 0) return Result<IReadOnlyList<Attachment>>.Ok([]);
        if (ids.Count > options.Value.MaxFilesPerMessage || ids.Distinct().Count() != ids.Count) return AttachmentsErrors.AttachmentLimit;
        var files = new List<Attachment>();
        foreach (var id in ids)
        {
            var file = await OwnedAsync(owner, id, ct);
            if (!file.IsSuccess) return file.Error;
            files.Add(file.Value);
        }
        if (files.Any(x => x.ContentType == "application/pdf" && string.IsNullOrWhiteSpace(x.ExtractedText))) return AttachmentsErrors.ProcessingRequired;
        if (files.Sum(x => x.Size) > options.Value.MaxMessageBytes) return AttachmentsErrors.TotalLimit;
        return files;
    }
}
