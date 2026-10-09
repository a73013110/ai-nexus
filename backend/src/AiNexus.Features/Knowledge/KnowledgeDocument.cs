using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Knowledge;

/// <summary>
/// A source document: in a collection (<see cref="CollectionId"/>), or a standalone document of a conversation or project.
/// Its name, owner and project live on the <see cref="WorkspaceResource"/> with the same id. Text sources keep their
/// editable text and a <see cref="TextVersion"/> that guards concurrent edits.
/// </summary>
public sealed class KnowledgeDocument
{
    public const string Kind = "document";

    public Guid Id { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? AttachmentId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Status { get; set; } = "queued";
    public int PageCount { get; set; }
    public int ChunkCount { get; set; }
    public string? Warning { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? JobId { get; set; }
    public string? TextContent { get; set; }
    public int TextVersion { get; set; }
}
public sealed class DocumentPage
{
    public Guid DocumentId { get; set; }
    public int PageNumber { get; set; }
    public string Text { get; set; } = "";
    public string Extraction { get; set; } = "native";
    public bool NeedsReview { get; set; }
}

public sealed record DocumentDto(Guid Id, Guid? CollectionId, string FileName, string ContentType, string Status, int PageCount, int ChunkCount, string? Warning, Guid? JobId, bool CanEdit, bool HasOriginal, int TextVersion = 0);
public sealed record DocumentPageDto(int PageNumber, string Text, string Extraction, bool NeedsReview);
public sealed record DocumentJobDto(JobDto Job, bool CanControl);

internal static class KnowledgeErrors
{
    public static readonly Error DocumentNotFound = Error.NotFound("document_not_found");
    public static readonly Error DescriptionTooLong = Error.Invalid("description_too_long");
    public static readonly Error CollectionLimit = Error.Conflict("collection_limit");
    public static readonly Error DocumentLimit = Error.Conflict("document_limit");
    public static readonly Error DocumentNotIndexed = Error.Conflict("document_not_indexed");
    public static readonly Error JobActive = Error.Conflict("job_active");
    public static readonly Error NotEditableText = Error.Conflict("document_not_editable_text");
    public static readonly Error TextTitleInvalid = Error.Invalid("text_title_invalid");
    public static readonly Error TextVersionChanged = Error.Conflict("text_version_changed");
    public static readonly Error DocumentProcessing = Error.Conflict("document_processing");
    public static readonly Error GenerationActive = Error.Conflict("generation_active");

    /// <summary>For <see cref="DocumentService"/>, whose callers in other modules and job handlers can only fail by exception.</summary>
    public static ApiException ToException(this Error error)
    {
        var status = Problems.Status(error.Kind);
        return new(status, error.Code, PublicErrorCatalog.Message(error.Code, status));
    }
}

/// <summary>
/// Who may read or change a document. A collection document follows its collection's ACL and needs the knowledge
/// feature; a standalone document follows its own resource (and its project's ACL when it belongs to one).
/// Missing documents and missing features are <see cref="KnowledgeErrors.DocumentNotFound"/>; ACL failures stay
/// exceptions of <see cref="ResourceAccess"/>.
/// </summary>
internal sealed class DocumentAccess(NexusDbContext db, ResourceAccess access, AccessService features)
{
    /// <summary>The tracked document.</summary>
    public async Task<Result<KnowledgeDocument>> FindAsync(Guid actor, Guid id, CancellationToken ct, bool write = false)
    {
        var doc = await db.Set<KnowledgeDocument>().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (doc is null) return KnowledgeErrors.DocumentNotFound;
        if (doc.CollectionId is Guid collection)
        {
            if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "knowledge")) return KnowledgeErrors.DocumentNotFound;
            await access.RequireAsync(actor, collection, KnowledgeCollection.Kind, ct, write);
        }
        else
        {
            if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id is "chat" or "knowledge" or "projects")) return KnowledgeErrors.DocumentNotFound;
            var resource = await access.RequireAsync(actor, doc.Id, KnowledgeDocument.Kind, ct, write);
            if (resource.ParentId is Guid parent)
            {
                if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "projects")) return KnowledgeErrors.DocumentNotFound;
                await access.RequireAsync(actor, parent, "project", ct, write);
            }
        }
        return doc;
    }

    public async Task<Result<DocumentDto>> DetailAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var document = await FindAsync(actor, id, ct);
        if (!document.IsSuccess) return document.Error;
        return await DescribeAsync(actor, document.Value, ct);
    }

    /// <summary>
    /// <see cref="DetailAsync"/> for a list, in the list's order and with the same checks and failures, in a fixed number
    /// of queries: the first document that fails fails the list.
    /// </summary>
    public async Task<Result<IReadOnlyList<DocumentDto>>> DetailsAsync(Guid actor, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return Array.Empty<DocumentDto>();
        var docs = await db.Set<KnowledgeDocument>().AsNoTracking().Where(x => ids.Contains(x.Id) && !x.IsDeleted).ToDictionaryAsync(x => x.Id, ct);
        var granted = (await features.ForUserAsync(actor, ct)).Features.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var collections = docs.Values.Where(x => x.CollectionId != null).Select(x => x.CollectionId!.Value).Distinct().ToArray();
        var standalone = docs.Values.Where(x => x.CollectionId == null).Select(x => x.Id).ToArray();
        // The resources DescribeAsync reports on: the collection, or the standalone document itself.
        var readable = new Dictionary<Guid, WorkspaceResource>();
        if (collections.Length > 0)
            foreach (var item in await (await access.QueryAsync(actor, KnowledgeCollection.Kind, ct)).AsNoTracking().Where(x => collections.Contains(x.Id)).ToListAsync(ct)) readable[item.Id] = item;
        if (standalone.Length > 0)
            foreach (var item in await (await access.QueryAsync(actor, KnowledgeDocument.Kind, ct)).AsNoTracking().Where(x => standalone.Contains(x.Id)).ToListAsync(ct)) readable[item.Id] = item;
        var parents = readable.Values.Where(x => x.Kind == KnowledgeDocument.Kind && x.ParentId != null).Select(x => x.ParentId!.Value).Distinct().ToArray();
        var projects = parents.Length == 0 ? [] : await (await access.QueryAsync(actor, "project", ct)).Where(x => parents.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        var resources = new List<WorkspaceResource>(ids.Count);
        foreach (var id in ids)
        {
            if (!docs.TryGetValue(id, out var doc)) return KnowledgeErrors.DocumentNotFound;
            if (doc.CollectionId is Guid collection)
            {
                if (!granted.Contains("knowledge")) return KnowledgeErrors.DocumentNotFound;
                resources.Add(readable.GetValueOrDefault(collection) ?? throw ResourceAccess.Missing());
            }
            else
            {
                if (!granted.Contains("chat") && !granted.Contains("knowledge") && !granted.Contains("projects")) return KnowledgeErrors.DocumentNotFound;
                var resource = readable.GetValueOrDefault(doc.Id) ?? throw ResourceAccess.Missing();
                if (resource.ParentId is Guid parent)
                {
                    if (!granted.Contains("projects")) return KnowledgeErrors.DocumentNotFound;
                    if (!projects.Contains(parent)) throw ResourceAccess.Missing();
                }
                resources.Add(resource);
            }
        }
        var editable = await access.EditableAsync(actor, resources.DistinctBy(x => x.Id).ToArray(), ct);
        var jobs = docs.Values.Where(x => x.JobId != null).Select(x => x.JobId!.Value).Distinct().ToArray();
        var states = jobs.Length == 0 ? [] : await db.Set<BackgroundJob>().Where(x => jobs.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Status, ct);
        return ids.Select((id, index) => Describe(docs[id], editable.Contains(resources[index].Id), docs[id].JobId is Guid job ? states.GetValueOrDefault(job) : null)).ToArray();
    }

    /// <summary>The stored original of a document the actor may read.</summary>
    public async Task<Result<Attachment>> OriginalAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var document = await FindAsync(actor, id, ct);
        if (!document.IsSuccess) return document.Error;
        if (document.Value.AttachmentId is not Guid attachment) return KnowledgeErrors.DocumentNotFound;
        var file = await db.Set<Attachment>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachment && x.StorageState == AttachmentStates.Ready, ct);
        if (file is null) return KnowledgeErrors.DocumentNotFound;
        return file;
    }

    public async Task<DocumentDto> DescribeAsync(Guid actor, KnowledgeDocument doc, CancellationToken ct)
    {
        var resource = doc.CollectionId is Guid collection ? await access.RequireAsync(actor, collection, KnowledgeCollection.Kind, ct) : await access.RequireAsync(actor, doc.Id, KnowledgeDocument.Kind, ct);
        var editable = (await access.DescribeAsync(actor, resource, ct)).CanEdit;
        var state = doc.JobId is Guid job ? await db.Set<BackgroundJob>().Where(x => x.Id == job).Select(x => x.Status).SingleOrDefaultAsync(ct) : null;
        return Describe(doc, editable, state);
    }

    /// <summary>A ready document always reads as ready; otherwise the state of its current job wins over the stored status.</summary>
    public static DocumentDto Describe(KnowledgeDocument x, bool edit, string? jobStatus = null) => new(x.Id, x.CollectionId, x.FileName, x.ContentType, x.Status == "ready" ? "ready" : jobStatus ?? x.Status, x.PageCount, x.ChunkCount, x.Warning, x.JobId, edit, x.AttachmentId != null, x.TextVersion);
}

internal sealed class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> document)
    {
        document.ToTable("Documents", "knowledge"); document.HasKey(x => x.Id);
        document.Property(x => x.FileName).HasMaxLength(180); document.Property(x => x.ContentType).HasMaxLength(80); document.Property(x => x.Status).HasMaxLength(16); document.Property(x => x.Warning).HasMaxLength(500);
        document.Property(x => x.TextContent).HasMaxLength(200000); document.Property(x => x.TextVersion).IsConcurrencyToken();
        document.HasIndex(x => new { x.CollectionId, x.Status, x.IsDeleted }).IncludeProperties(x => new { x.Id, x.FileName, x.ChunkCount }); document.HasIndex(x => x.AttachmentId);
        document.HasOne<KnowledgeCollection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DocumentPageConfiguration : IEntityTypeConfiguration<DocumentPage>
{
    public void Configure(EntityTypeBuilder<DocumentPage> page)
    {
        page.ToTable("DocumentPages", "knowledge"); page.HasKey(x => new { x.DocumentId, x.PageNumber }); page.Property(x => x.Extraction).HasMaxLength(16);
        page.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}
