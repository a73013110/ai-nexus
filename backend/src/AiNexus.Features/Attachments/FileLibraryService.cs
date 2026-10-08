using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Attachments;

public sealed record FileUsageDto(string Kind, Guid ResourceId, string Name, Guid? DocumentId, string? Status);
public sealed record LibraryFileDto(AttachmentDto File, DateTimeOffset CreatedAt, IReadOnlyList<FileUsageDto> Usages, bool CanDelete);
public sealed record FileLibraryPageDto(IReadOnlyList<LibraryFileDto> Items, int Total, AttachmentStorageDto Storage, int Offset, int Limit);
public sealed record RenameLibraryFileRequest(string FileName, string ExpectedFileName);

/// <summary>The original is stored once. History and knowledge index it through independent references.</summary>
public sealed class FileLibraryService(NexusDbContext db, AttachmentService files, AttachmentWriteLock writes, ResourceAccess access, AccessService features, AttachmentQuota quota)
{
    public async Task<AttachmentDto> RenameAsync(Guid actor, Guid id, RenameLibraryFileRequest request, CancellationToken ct)
    {
        var name = request.FileName.Trim();
        if (name.Length is < 1 or > 180 || name.Any(char.IsControl) || name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || name.EndsWith('.') || name is "." or "..")
            throw new ApiException(400, "file_name_invalid", "檔名需為 1 至 180 個字元，不可包含路徑、控制字元或特殊符號。");
        var file = await files.OwnedAsync(actor, id, ct);
        if (!file.InLibrary) throw new ApiException(404, "file_not_found", "找不到這份檔案。");
        if (!string.Equals(Path.GetExtension(name), Path.GetExtension(file.FileName), StringComparison.OrdinalIgnoreCase))
            throw new ApiException(400, "file_extension_changed", "重新命名時請保留原副檔名，以維持正確的預覽與格式識別。");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.Set<Attachment>().Where(x => x.Id == id && x.OwnerId == actor && x.InLibrary && x.FileName == request.ExpectedFileName && x.StorageState == AttachmentStates.Ready)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.FileName, name), ct);
        if (changed != 1) throw new ApiException(409, "file_name_changed", "檔名已被修改，請重新載入後再試。");
        db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "file.renamed", Result = "saved" });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        file.FileName = name;
        return AttachmentService.Describe(file);
    }
    public async Task RetainAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await quota.LockOwnerAsync(actor, ct);
            var file = await files.OwnedAsync(actor, id, ct);
            if (file.InLibrary) return;
            await db.Set<Attachment>().Where(x => x.Id == id && x.OwnerId == actor).ExecuteUpdateAsync(p => p.SetProperty(x => x.InLibrary, true), ct);
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "file.retained", Result = "saved" });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
    }

    public async Task<FileLibraryPageDto> ListAsync(Guid actor, string? search, string type, string source, int offset, int limit, CancellationToken ct)
    {
        if (search?.Length > 180 || type is not ("all" or "images" or "documents") || source is not ("all" or "chat" or "knowledge" or "projects" or "library") || offset is < 0 or > 100000 || limit is < 1 or > 60)
            throw new ApiException(400, "file_filter_invalid", "檔案查詢條件不正確。");
        var grants = (await features.ForUserAsync(actor, ct)).Features.Select(x => x.Id).ToHashSet();
        var collections = grants.Contains("knowledge") ? (await access.QueryAsync(actor, "knowledge", ct)).Select(x => x.Id) : db.Set<WorkspaceResource>().Where(x => false).Select(x => x.Id);
        var projects = grants.Contains("projects") ? (await access.QueryAsync(actor, "project", ct)).Select(x => x.Id) : db.Set<WorkspaceResource>().Where(x => false).Select(x => x.Id);
        var chatFiles = from link in db.Set<MessageAttachment>() join message in db.Messages on link.MessageId equals message.Id
            join conversation in db.Conversations on message.ConversationId equals conversation.Id
            where conversation.OwnerId == actor && !conversation.IsDeleted && grants.Contains("chat")
            select new { link.AttachmentId, conversation.Id, conversation.Title };
        var resourceFiles = from doc in db.Set<KnowledgeDocument>() join resource in db.Set<WorkspaceResource>() on doc.Id equals resource.Id
            where !doc.IsDeleted && !resource.IsDeleted && doc.AttachmentId != null
            select new { AttachmentId = doc.AttachmentId!.Value, doc.CollectionId, resource.ParentId, DocumentId = doc.Id, doc.Status };
        var knowledgeFiles = resourceFiles.Where(x => x.CollectionId != null && collections.Contains(x.CollectionId.Value));
        var projectFiles = resourceFiles.Where(x => x.ParentId != null && projects.Contains(x.ParentId.Value));
        var privateReaders = from doc in db.Set<KnowledgeDocument>() join resource in db.Set<WorkspaceResource>() on doc.Id equals resource.Id
            where !doc.IsDeleted && !resource.IsDeleted && doc.CollectionId == null && resource.ParentId == null && resource.OwnerId == actor select doc.Id;
        var owned = db.Set<Attachment>().AsNoTracking().Where(x => x.OwnerId == actor && x.InLibrary && x.StorageState == AttachmentStates.Ready);
        var storage = await quota.ForAsync(actor, ct);
        var query = owned;
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.FileName.Contains(search.Trim()));
        if (type == "images") query = query.Where(x => x.ContentType.StartsWith("image/"));
        if (type == "documents") query = query.Where(x => !x.ContentType.StartsWith("image/"));
        if (source == "chat") query = query.Where(x => chatFiles.Any(l => l.AttachmentId == x.Id));
        if (source == "knowledge") query = query.Where(x => knowledgeFiles.Any(l => l.AttachmentId == x.Id));
        if (source == "projects") query = query.Where(x => projectFiles.Any(l => l.AttachmentId == x.Id));
        if (source == "library") query = query.Where(x => !chatFiles.Any(l => l.AttachmentId == x.Id) && !resourceFiles.Any(l => l.AttachmentId == x.Id && (l.CollectionId != null || l.ParentId != null)));
        var total = await query.CountAsync(ct);
        // Metadata only: never load binary files or extracted document text for a library listing.
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip(offset).Take(limit)
            .Select(x => new { x.Id, x.FileName, x.ContentType, x.Size, x.CreatedAt,
                HasText = x.ExtractedText != null && x.ExtractedText != "",
                CanDelete = !db.Set<MessageAttachment>().Any(l => l.AttachmentId == x.Id) && !db.Set<AttachmentReference>().Any(l => l.AttachmentId == x.Id && !privateReaders.Contains(l.ResourceId)) }).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var usage = new List<(Guid FileId, FileUsageDto Value)>();
        var chats = await chatFiles.Where(x => ids.Contains(x.AttachmentId)).Distinct().OrderBy(x => x.Id).Take(500).ToListAsync(ct);
        usage.AddRange(chats.Select(x => (x.AttachmentId, new FileUsageDto("chat", x.Id, x.Title, null, null))));
        var knowledge = await (from link in knowledgeFiles join resource in db.Set<WorkspaceResource>() on link.CollectionId equals resource.Id
            where ids.Contains(link.AttachmentId) orderby resource.Name select new { link.AttachmentId, resource.Id, resource.Name, link.DocumentId, link.Status }).Take(500).ToListAsync(ct);
        usage.AddRange(knowledge.Select(x => (x.AttachmentId, new FileUsageDto("knowledge", x.Id, x.Name, x.DocumentId, x.Status))));
        var project = await (from link in projectFiles join resource in db.Set<WorkspaceResource>() on link.ParentId equals resource.Id
            where ids.Contains(link.AttachmentId) orderby resource.Name select new { link.AttachmentId, resource.Id, resource.Name, link.DocumentId, link.Status }).Take(500).ToListAsync(ct);
        usage.AddRange(project.Select(x => (x.AttachmentId, new FileUsageDto("projects", x.Id, x.Name, x.DocumentId, x.Status))));
        var byFile = usage.ToLookup(x => x.FileId, x => x.Value);
        return new(rows.Select(x => new LibraryFileDto(new(x.Id, x.FileName, x.ContentType, x.Size, x.ContentType.StartsWith("image/"),
            x.ContentType == "application/pdf" && !x.HasText ? "ocr-required" : x.ContentType.StartsWith("image/") && !x.HasText ? "vision" : "extracted-text"), x.CreatedAt, byFile[x.Id].Take(8).ToArray(), x.CanDelete)).ToArray(), total, storage, offset, limit);
    }
}
