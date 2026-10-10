using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>The documents of a collection the user may read, by file name, with the state of their current job.</summary>
internal sealed class ListKnowledgeDocuments(NexusDbContext db, ResourceAccess access, IOptions<KnowledgeOptions> options)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/collections/{id:guid}/documents", (Guid id, ICurrentUser user, ListKnowledgeDocuments handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("ListKnowledgeDocuments");

    public async Task<Result<IReadOnlyList<DocumentDto>>> HandleAsync(Guid actor, Guid collection, CancellationToken ct)
    {
        var resource = await access.RequireAsync(actor, collection, KnowledgeCollection.Kind, ct);
        if (!resource.IsSuccess) return resource.Error;
        var editable = (await access.DescribeAsync(actor, resource.Value, ct)).CanEdit;
        var docs = await db.Set<KnowledgeDocument>().AsNoTracking().Where(x => x.CollectionId == collection && !x.IsDeleted).OrderBy(x => x.FileName).Take(options.Value.Indexing.MaxDocumentsPerCollection).ToListAsync(ct);
        var ids = docs.Select(x => x.JobId).ToArray();
        var states = await db.Set<BackgroundJob>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Status, ct);
        return docs.Select(x => DocumentAccess.Describe(x, editable, x.JobId is Guid job && states.TryGetValue(job, out var state) ? state : null)).ToList();
    }
}
