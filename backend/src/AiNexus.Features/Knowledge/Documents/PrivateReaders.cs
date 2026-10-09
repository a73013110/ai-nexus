using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Indexing;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>Standalone documents outside any collection and project; their resources are read past the soft-delete filter.</summary>
internal sealed class PrivateReaders(NexusDbContext db) : IPrivateReaders
{
    public IQueryable<PrivateReader> All => from doc in db.Set<KnowledgeDocument>() join resource in db.Set<WorkspaceResource>().IgnoreQueryFilters([SoftDelete.Filter]) on doc.Id equals resource.Id
        where doc.CollectionId == null && resource.ParentId == null select new PrivateReader { Id = doc.Id, OwnerId = resource.OwnerId };

    public async Task RemoveAsync(Guid owner, IReadOnlyList<Guid> files, CancellationToken ct)
    {
        var readers = All.Where(x => x.OwnerId == owner).Select(x => x.Id);
        var ids = await db.Set<KnowledgeDocument>().Where(x => files.Contains(x.AttachmentId ?? Guid.Empty) && readers.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(ct);
        await db.Set<KnowledgeDocument>().Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true).SetProperty(x => x.Status, "deleted").SetProperty(x => x.AttachmentId, (Guid?)null), ct);
        await db.Set<WorkspaceResource>().Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true), ct);
        await db.Set<BackgroundJob>().Where(x => ids.Contains(x.SubjectId) && x.ActiveKey != null).ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true), ct);
        await db.Set<KnowledgeChunk>().Where(x => ids.Contains(x.DocumentId)).ExecuteDeleteAsync(ct);
        await db.Set<DocumentPage>().Where(x => ids.Contains(x.DocumentId)).ExecuteDeleteAsync(ct);
        await db.Set<AttachmentReference>().Where(x => ids.Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
    }
}
