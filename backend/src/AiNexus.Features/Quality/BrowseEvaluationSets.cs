using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Http;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Quality;

/// <summary>Sets the user may read: the latest 100, and one set with its cases.</summary>
internal static class BrowseEvaluationSets
{
    public static RouteHandlerBuilder MapList(RouteGroupBuilder routes) => routes
        .MapGet("/sets", async (ICurrentUser user, NexusDbContext db, ResourceAccess access, CancellationToken ct) => Results.Ok(await ListAsync(user.Id, db, access, ct)))
        .Produces<IReadOnlyList<EvaluationSetDto>>().WithRequestBodyLimit(QualityModule.SetBodyLimit);

    public static RouteHandlerBuilder MapGet(RouteGroupBuilder routes) => routes
        .MapGet("/sets/{id:guid}", async (Guid id, ICurrentUser user, NexusDbContext db, ResourceAccess access, CancellationToken ct) => Results.Ok(await access.LoadSetAsync(db, user.Id, id, ct)))
        .Produces<EvaluationSetDto>().WithRequestBodyLimit(QualityModule.SetBodyLimit);

    private static async Task<IReadOnlyList<EvaluationSetDto>> ListAsync(Guid actor, NexusDbContext db, ResourceAccess access, CancellationToken ct)
    {
        var resources = await (await access.QueryAsync(actor, EvaluationSet.Kind, ct)).AsNoTracking().OrderByDescending(x => x.UpdatedAt).Take(100).ToListAsync(ct);
        var ids = resources.Select(x => x.Id).ToArray();
        var sets = await db.Set<EvaluationSet>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return (await access.DescribeAllAsync(actor, resources, ct)).Select(resource => sets[resource.Id].ToDto(resource)).ToArray();
    }
}
