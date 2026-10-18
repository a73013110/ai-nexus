using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Work.Files.Storage;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Knowledge.Documents;

/// <summary>不屬於任何知識庫與專案的獨立文件；讀取其 Resource 時需略過軟刪除過濾。</summary>
internal sealed class PrivateReaders(AppDbContext db) : IPrivateReaders
{
    public IQueryable<PrivateReader> All => from doc in db.Set<Document>() join resource in db.Set<Resource>().IgnoreQueryFilters([SoftDelete.Filter]) on doc.Id equals resource.Id
        where doc.CollectionId == null && resource.ParentId == null select new PrivateReader { Id = doc.Id, OwnerId = resource.OwnerId };

    public async Task RemoveAsync(Guid owner, IReadOnlyList<Guid> files, CancellationToken ct)
    {
        var readers = All.Where(x => x.OwnerId == owner).Select(x => x.Id);
        var ids = await db.Set<Document>().Where(x => files.Contains(x.AttachmentId ?? Guid.Empty) && readers.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(ct);
        await db.Set<Document>().Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true).SetProperty(x => x.Status, "deleted").SetProperty(x => x.AttachmentId, (Guid?)null), ct);
        await db.Set<Resource>().Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true), ct);
        await db.Set<BackgroundJob>().Where(x => ids.Contains(x.SubjectId) && x.ActiveKey != null).ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true), ct);
        await db.Set<Chunk>().Where(x => ids.Contains(x.DocumentId)).ExecuteDeleteAsync(ct);
        await db.Set<DocumentPage>().Where(x => ids.Contains(x.DocumentId)).ExecuteDeleteAsync(ct);
        await db.Set<ResourceAttachment>().Where(x => ids.Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
    }
}
