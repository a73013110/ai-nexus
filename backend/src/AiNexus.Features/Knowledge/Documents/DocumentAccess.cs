using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Collections;

namespace AiNexus.Features.Knowledge.Documents;

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
            (await access.RequireAsync(actor, collection, KnowledgeCollection.Kind, ct, write)).OrThrow();
        }
        else
        {
            if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id is "chat" or "knowledge" or "projects")) return KnowledgeErrors.DocumentNotFound;
            var resource = (await access.RequireAsync(actor, doc.Id, KnowledgeDocument.Kind, ct, write)).OrThrow();
            if (resource.ParentId is Guid parent)
            {
                if (!(await features.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "projects")) return KnowledgeErrors.DocumentNotFound;
                (await access.RequireAsync(actor, parent, "project", ct, write)).OrThrow();
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
                resources.Add(readable.GetValueOrDefault(collection) ?? throw CollaborationErrors.ResourceNotFound.Throwable());
            }
            else
            {
                if (!granted.Contains("chat") && !granted.Contains("knowledge") && !granted.Contains("projects")) return KnowledgeErrors.DocumentNotFound;
                var resource = readable.GetValueOrDefault(doc.Id) ?? throw CollaborationErrors.ResourceNotFound.Throwable();
                if (resource.ParentId is Guid parent)
                {
                    if (!granted.Contains("projects")) return KnowledgeErrors.DocumentNotFound;
                    if (!projects.Contains(parent)) throw CollaborationErrors.ResourceNotFound.Throwable();
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
        var resource = doc.CollectionId is Guid collection ? (await access.RequireAsync(actor, collection, KnowledgeCollection.Kind, ct)).OrThrow() : (await access.RequireAsync(actor, doc.Id, KnowledgeDocument.Kind, ct)).OrThrow();
        var editable = (await access.DescribeAsync(actor, resource, ct)).CanEdit;
        var state = doc.JobId is Guid job ? await db.Set<BackgroundJob>().Where(x => x.Id == job).Select(x => x.Status).SingleOrDefaultAsync(ct) : null;
        return Describe(doc, editable, state);
    }

    /// <summary>A ready document always reads as ready; otherwise the state of its current job wins over the stored status.</summary>
    public static DocumentDto Describe(KnowledgeDocument x, bool edit, string? jobStatus = null) => new(x.Id, x.CollectionId, x.FileName, x.ContentType, x.Status == "ready" ? "ready" : jobStatus ?? x.Status, x.PageCount, x.ChunkCount, x.Warning, x.JobId, edit, x.AttachmentId != null, x.TextVersion);
}
