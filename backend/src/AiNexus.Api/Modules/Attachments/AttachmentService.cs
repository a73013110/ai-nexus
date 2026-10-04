using AiNexus.BuildingBlocks;
using AiNexus.Database;
using EDoc.Core.Database.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Attachments;

public sealed class AttachmentService(IEfHelper<INexusDatabase> ef, DocumentExtractor extractor, IOptions<AttachmentOptions> options, AttachmentWriteLock writes, AiNexus.Modules.Administration.ModelPolicyService policies)
{
    public AttachmentPolicyDto Policy => new(options.Value.MaxFileBytes, options.Value.MaxFilesPerMessage, options.Value.MaxMessageBytes, DocumentExtractor.Extensions);
    public static AttachmentDto Describe(Attachment file) => new(file.Id, file.FileName, file.ContentType, file.Size, file.ContentType.StartsWith("image/"), file.ExtractedText is null ? "vision" : "extracted-text");

    public async Task<AttachmentDto> UploadAsync(Guid owner, IFormFile file, CancellationToken ct)
    {
        if (file.Length is < 1 || file.Length > options.Value.MaxFileBytes) throw new ApiException(413, "file_size_limit", $"每個檔案最多 {options.Value.MaxFileBytes / 1024 / 1024} MB。");
        var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (name.Length is < 1 or > 180 || name.Any(char.IsControl)) throw new ApiException(400, "invalid_file_name", "檔案名稱不正確或過長。");
        using var data = new MemoryStream();
        await file.CopyToAsync(data, ct);
        if (data.Length > options.Value.MaxFileBytes) throw new ApiException(413, "file_size_limit", "檔案過大。");
        var bytes = data.ToArray();
        var (type, text) = extractor.Extract(name, bytes, ct);
        await writes.Gate.WaitAsync(ct);
        try
        {
            // Reclaim interrupted uploads and abandoned browser drafts before checking quota.
            // The write gate also covers generation binding; linked history is never eligible.
            var cutoff = DateTimeOffset.UtcNow.AddDays(-options.Value.DraftRetentionDays);
            await ef.Set<Attachment>().Where(x => x.OwnerId == owner && x.CreatedAt < cutoff && !ef.Set<MessageAttachment>().Any(link => link.AttachmentId == x.Id)).ExecuteDeleteAsync(ct);
            var used = await ef.Set<Attachment>().Where(x => x.OwnerId == owner).SumAsync(x => (long?)x.Size, ct) ?? 0;
            var groupLimit = (await policies.ForAsync(owner, ct)).StoredAttachmentLimitBytes ?? options.Value.MaxOwnerBytes;
            if (used + bytes.Length > Math.Min(groupLimit, options.Value.MaxOwnerBytes)) throw new ApiException(413, "attachment_quota", "個人附件空間已達系統或群組上限，請移除尚未使用的附件或聯絡管理員。");
            var attachment = new Attachment { OwnerId = owner, FileName = name, ContentType = type, Data = bytes, Size = bytes.Length, ExtractedText = text };
            ef.Set<Attachment>().Add(attachment);
            await ef.SaveChangesAsync(ct);
            return Describe(attachment);
        }
        finally { writes.Gate.Release(); }
    }

    public async Task<Attachment> OwnedAsync(Guid owner, Guid id, CancellationToken ct, bool includeData = true)
    {
        var query = ef.Set<Attachment>().AsNoTracking().Where(x => x.Id == id && x.OwnerId == owner);
        var file = await (includeData ? query : query.Select(x => new Attachment { Id = x.Id, OwnerId = x.OwnerId, FileName = x.FileName, ContentType = x.ContentType, Size = x.Size, ExtractedText = x.ExtractedText, CreatedAt = x.CreatedAt })).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(404, "attachment_not_found", "找不到這個附件。");
        if (await ef.Set<MessageAttachment>().AnyAsync(x => x.AttachmentId == id, ct) &&
            !await (from link in ef.Set<MessageAttachment>() join message in ef.Set<AiNexus.Modules.Conversations.Message>() on link.MessageId equals message.Id join conversation in ef.Set<AiNexus.Modules.Conversations.Conversation>() on message.ConversationId equals conversation.Id where link.AttachmentId == id && !conversation.IsDeleted && conversation.OwnerId == owner select link).AnyAsync(ct))
            throw new ApiException(404, "attachment_not_found", "附件所屬對話已刪除。");
        return file;
    }

    public async Task<IReadOnlyList<Attachment>> RequireAsync(Guid owner, IReadOnlyList<Guid>? ids, CancellationToken ct)
    {
        if (ids is null || ids.Count == 0) return [];
        if (ids.Count > options.Value.MaxFilesPerMessage || ids.Distinct().Count() != ids.Count) throw new ApiException(400, "attachment_limit", $"每則提問最多 {options.Value.MaxFilesPerMessage} 個不同附件。");
        var files = new List<Attachment>();
        foreach (var id in ids) files.Add(await OwnedAsync(owner, id, ct, includeData: false));
        if (files.Sum(x => x.Size) > options.Value.MaxMessageBytes) throw new ApiException(413, "attachment_total_limit", "這次附件總大小超過上限。");
        return files;
    }

    public async Task RemoveDraftAsync(Guid owner, Guid id, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            var file = await OwnedAsync(owner, id, ct);
            if (await ef.Set<MessageAttachment>().AnyAsync(x => x.AttachmentId == id, ct)) throw new ApiException(409, "attachment_in_use", "已送出的附件由對話保留，無法單獨刪除。");
            ef.Set<Attachment>().Remove(file);
            await ef.SaveChangesAsync(ct);
        }
        finally { writes.Gate.Release(); }
    }
}
