using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Knowledge;

/// <summary>The documents of a collection the user may read, by file name, with the state of their current job.</summary>
internal static class ListKnowledgeDocuments
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/collections/{id:guid}/documents", async (Guid id, ICurrentUser user, NexusDbContext db, ResourceAccess access, IOptions<KnowledgeOptions> options, CancellationToken ct) =>
            Results.Ok(await HandleAsync(db, access, options.Value, user.Id, id, ct)))
        .WithName("ListKnowledgeDocuments").Produces<IReadOnlyList<DocumentDto>>();

    private static async Task<IReadOnlyList<DocumentDto>> HandleAsync(NexusDbContext db, ResourceAccess access, KnowledgeOptions options, Guid actor, Guid collection, CancellationToken ct)
    {
        var resource = await access.RequireAsync(actor, collection, KnowledgeCollection.Kind, ct);
        var editable = (await access.DescribeAsync(actor, resource, ct)).CanEdit;
        var docs = await db.Set<KnowledgeDocument>().AsNoTracking().Where(x => x.CollectionId == collection && !x.IsDeleted).OrderBy(x => x.FileName).Take(options.MaxDocumentsPerCollection).ToListAsync(ct);
        var ids = docs.Select(x => x.JobId).ToArray();
        var states = await db.Set<BackgroundJob>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Status, ct);
        return docs.Select(x => DocumentAccess.Describe(x, editable, x.JobId is Guid job && states.TryGetValue(job, out var state) ? state : null)).ToList();
    }
}
