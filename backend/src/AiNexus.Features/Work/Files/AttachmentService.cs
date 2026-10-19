using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Data.App.Conversations;
using AiNexus.Features.Work.Files.Extraction;
using AiNexus.Features.Work.Files.Storage;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Files;

/// <summary>
/// 檔案模組（Files）對其他模組的契約：上傳、擁有者查詢、配額鎖定與讀取儲存位元組。
/// 儲存的檔案若遺失或與記錄不符，屬於基礎設施錯誤，會直接拋出例外。
/// </summary>
public sealed class AttachmentService(DocumentExtractor extractor, IOptions<FilesOptions> options, AttachmentWriteLock writes, AppDbContext db, IAttachmentStorage storage, AttachmentQuota quota, AttachmentLifecycle lifecycle, TimeProvider clock)
{
    public static AttachmentDto Describe(Attachment file) => new(file.Id, file.FileName, file.ContentType, file.Size, file.ContentType.StartsWith("image/", StringComparison.Ordinal), NeedsOcr(file) ? "ocr-required" : file.ExtractedText is null ? "vision" : "extracted-text");

    // 掃描型 PDF 抽不到文字，要先 OCR 才能送進對話；顯示模式與送出檢查共用同一個判斷。
    private static bool NeedsOcr(Attachment file) => file.ContentType == "application/pdf" && string.IsNullOrWhiteSpace(file.ExtractedText);

    public async Task<Result<AttachmentDto>> UploadAsync(Guid owner, IFormFile file, CancellationToken ct)
    {
        if (file.Length is < 1 || file.Length > options.Value.MaxFileBytes) return FilesErrors.FileSizeLimit;
        var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (name.Length is < 1 or > Attachment.FileNameMaxLength || name.Any(char.IsControl)) return FilesErrors.InvalidFileName;
        using var data = new MemoryStream();
        await using (var input = file.OpenReadStream())
        {
            var buffer = new byte[65536];
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (data.Length + read > options.Value.MaxFileBytes) return FilesErrors.FileSizeLimit;
                await data.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        if (data.Length == 0 || data.Length != file.Length) return FilesErrors.FileSizeMismatch;
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
            return FilesErrors.UploadExpired;
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
            throw new ExternalServiceException(FilesErrors.ContentInvalid, "附件原檔與儲存記錄不符，請聯絡管理員檢查備份。");
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
        if (file is null) return FilesErrors.NotFound;
        if (!file.InLibrary && !await db.Set<ResourceAttachment>().AnyAsync(x => x.AttachmentId == id, ct) && await db.Set<MessageAttachment>().AnyAsync(x => x.AttachmentId == id, ct) &&
            !await (from link in db.Set<MessageAttachment>() join message in db.Set<Message>() on link.MessageId equals message.Id join conversation in db.Set<Conversation>() on message.ConversationId equals conversation.Id where link.AttachmentId == id && conversation.OwnerId == owner select link).AnyAsync(ct))
            return FilesErrors.NotFound;
        return file;
    }

    public async Task<Result<IReadOnlyList<Attachment>>> RequireAsync(Guid owner, IReadOnlyList<Guid>? ids, CancellationToken ct)
    {
        if (ids is null || ids.Count == 0) return Result<IReadOnlyList<Attachment>>.Ok([]);
        if (ids.Count > options.Value.MaxFilesPerMessage || ids.Distinct().Count() != ids.Count) return FilesErrors.AttachmentLimit;
        var files = new List<Attachment>();
        foreach (var id in ids)
        {
            var file = await OwnedAsync(owner, id, ct);
            if (!file.IsSuccess) return file.Error;
            files.Add(file.Value);
        }
        if (files.Any(NeedsOcr)) return FilesErrors.ProcessingRequired;
        if (files.Sum(x => x.Size) > options.Value.MaxMessageBytes) return FilesErrors.TotalLimit;
        return files;
    }
}
