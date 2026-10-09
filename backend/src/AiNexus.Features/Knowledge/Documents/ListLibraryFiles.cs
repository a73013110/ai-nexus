using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Attachments;

namespace AiNexus.Features.Knowledge.Documents;

public sealed record FileUsageDto(string Kind, Guid ResourceId, string Name, Guid? DocumentId, string? Status);

public sealed record LibraryFileDto(AttachmentDto File, DateTimeOffset CreatedAt, IReadOnlyList<FileUsageDto> Usages, bool CanDelete);

public sealed record FileLibraryPageDto(IReadOnlyList<LibraryFileDto> Items, int Total, AttachmentStorageDto Storage, int Offset, int Limit);

/// <summary>
/// The caller's library files with where each is used. The original is stored once; history and knowledge index it
/// through independent references, and only usages the caller can still open are listed.
/// </summary>
internal sealed class ListLibraryFiles(NexusDbContext db, ResourceAccess access, AccessService features, AttachmentQuota quota)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("", async (string? search, string? type, string? source, int? offset, int? limit, ICurrentUser user, ListLibraryFiles handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, search, type ?? "all", source ?? "all", offset ?? 0, limit ?? 40, ct)).ToHttpResult())
        .WithName("ListLibraryFiles").Produces<FileLibraryPageDto>();

    public async Task<Result<FileLibraryPageDto>> HandleAsync(Guid actor, string? search, string type, string source, int offset, int limit, CancellationToken ct)
    {
        if (search?.Length > 180 || type is not ("all" or "images" or "documents") || source is not ("all" or "chat" or "knowledge" or "projects" or "library") || offset is < 0 or > 100000 || limit is < 1 or > 60)
            return AttachmentsErrors.FilterInvalid;
        var grants = (await features.ForUserAsync(actor, ct)).Features.Select(x => x.Id).ToHashSet();
        var collections = grants.Contains("knowledge") ? (await access.QueryAsync(actor, "knowledge", ct)).Select(x => x.Id) : db.Set<WorkspaceResource>().Where(x => false).Select(x => x.Id);
        var projects = grants.Contains("projects") ? (await access.QueryAsync(actor, "project", ct)).Select(x => x.Id) : db.Set<WorkspaceResource>().Where(x => false).Select(x => x.Id);
        var chatFiles = from link in db.Set<MessageAttachment>() join message in db.Messages on link.MessageId equals message.Id
            join conversation in db.Conversations on message.ConversationId equals conversation.Id
            where conversation.OwnerId == actor && grants.Contains("chat")
            select new { link.AttachmentId, conversation.Id, conversation.Title };
        var resourceFiles = from doc in db.Set<KnowledgeDocument>() join resource in db.Set<WorkspaceResource>() on doc.Id equals resource.Id
            where !doc.IsDeleted && doc.AttachmentId != null
            select new { AttachmentId = doc.AttachmentId!.Value, doc.CollectionId, resource.ParentId, DocumentId = doc.Id, doc.Status };
        var knowledgeFiles = resourceFiles.Where(x => x.CollectionId != null && collections.Contains(x.CollectionId.Value));
        var projectFiles = resourceFiles.Where(x => x.ParentId != null && projects.Contains(x.ParentId.Value));
        var privateReaders = from doc in db.Set<KnowledgeDocument>() join resource in db.Set<WorkspaceResource>() on doc.Id equals resource.Id
            where !doc.IsDeleted && doc.CollectionId == null && resource.ParentId == null && resource.OwnerId == actor select doc.Id;
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
        return new FileLibraryPageDto(rows.Select(x => new LibraryFileDto(new(x.Id, x.FileName, x.ContentType, x.Size, x.ContentType.StartsWith("image/", StringComparison.Ordinal),
            x.ContentType == "application/pdf" && !x.HasText ? "ocr-required" : x.ContentType.StartsWith("image/", StringComparison.Ordinal) && !x.HasText ? "vision" : "extracted-text"), x.CreatedAt, byFile[x.Id].Take(8).ToArray(), x.CanDelete)).ToArray(), total, storage, offset, limit);
    }
}
