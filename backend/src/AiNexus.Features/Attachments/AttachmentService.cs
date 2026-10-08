using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Conversations;
using EDoc.Core.Database.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Attachments;

public sealed class AttachmentService(IEfHelper<INexusDatabase> ef, DocumentExtractor extractor, IOptions<AttachmentOptions> options, AttachmentWriteLock writes, NexusDbContext db, IAttachmentStorage storage, AttachmentQuota quota, AttachmentLifecycle lifecycle)
{
    public AttachmentPolicyDto Policy => new(options.Value.MaxFileBytes, options.Value.MaxFilesPerMessage, options.Value.MaxMessageBytes, DocumentExtractor.Extensions);
    public static AttachmentDto Describe(Attachment file) => new(file.Id, file.FileName, file.ContentType, file.Size, file.ContentType.StartsWith("image/"), file.ContentType == "application/pdf" && string.IsNullOrWhiteSpace(file.ExtractedText) ? "ocr-required" : file.ExtractedText is null ? "vision" : "extracted-text");

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
        await lifecycle.ReclaimAsync(ct);
        var attachment = new Attachment { OwnerId = owner, FileName = name, ContentType = type, Size = bytes.Length, ExtractedText = text, StorageState = AttachmentStates.Pending };
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await quota.ReserveAsync(owner, bytes.Length, ct);
            ef.Set<Attachment>().Add(attachment);
            await ef.SaveChangesAsync(ct);
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
        var file = await ef.Set<Attachment>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner && x.StorageState == AttachmentStates.Ready, ct)
            ?? throw new ApiException(404, "attachment_not_found", "找不到這個附件。");
        if (!file.InLibrary && !await ef.Set<AttachmentReference>().AnyAsync(x => x.AttachmentId == id, ct) && await ef.Set<MessageAttachment>().AnyAsync(x => x.AttachmentId == id, ct) &&
            !await (from link in ef.Set<MessageAttachment>() join message in ef.Set<AiNexus.Features.Conversations.Message>() on link.MessageId equals message.Id join conversation in ef.Set<AiNexus.Features.Conversations.Conversation>() on message.ConversationId equals conversation.Id where link.AttachmentId == id && !conversation.IsDeleted && conversation.OwnerId == owner select link).AnyAsync(ct))
            throw new ApiException(404, "attachment_not_found", "附件所屬對話已刪除。");
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

    public async Task RemoveDraftAsync(Guid owner, Guid id, CancellationToken ct, bool fromLibrary = false)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await quota.LockOwnerAsync(owner, ct);
            var file = await OwnedAsync(owner, id, ct);
            // Removing a reused file from the composer must never delete its library original.
            if (file.InLibrary && !fromLibrary) return;
            if (await ef.Set<MessageAttachment>().AnyAsync(x => x.AttachmentId == id, ct)) throw new ApiException(409, "attachment_in_use", "已送出的附件由對話保留，無法單獨刪除。");
            if (await ef.Set<AttachmentReference>().AnyAsync(x => x.AttachmentId == id && !lifecycle.PrivateReaders(owner).Contains(x.ResourceId), ct)) throw new ApiException(409, "attachment_in_use", "此附件由知識庫、專案或分享保留，請從該項目移除。");
            await lifecycle.RemovePrivateReadersAsync(owner, [id], ct);
            await db.Set<Attachment>().Where(x => x.Id == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, false).SetProperty(x => x.StorageState, AttachmentStates.Deleting), ct);
            if (fromLibrary) { db.AuditEvents.Add(new() { OwnerId = owner, ResourceId = id, Action = "file.deleted", Result = "deleted" }); await db.SaveChangesAsync(ct); }
            await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
        await lifecycle.DeletePendingAsync(ct, id);
    }
}
