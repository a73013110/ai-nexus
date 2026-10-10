using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Quality.Evaluations;

/// <summary>Sets the user may read: the latest 100, and one set with its cases.</summary>
internal sealed class BrowseEvaluationSets(NexusDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder MapList(RouteGroupBuilder routes) => routes
        .MapGet("/sets", async (ICurrentUser user, BrowseEvaluationSets handler, CancellationToken ct) => TypedResults.Ok(await handler.ListAsync(user.Id, ct)))
        .WithRequestBodyLimit(QualityModule.SetBodyLimit);

    public static RouteHandlerBuilder MapGet(RouteGroupBuilder routes) => routes
        .MapGet("/sets/{id:guid}", (Guid id, ICurrentUser user, BrowseEvaluationSets handler, CancellationToken ct) => handler.GetAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithRequestBodyLimit(QualityModule.SetBodyLimit);

    public Task<Result<EvaluationSetDto>> GetAsync(Guid actor, Guid id, CancellationToken ct) => access.LoadSetAsync(db, actor, id, ct);

    public async Task<IReadOnlyList<EvaluationSetDto>> ListAsync(Guid actor, CancellationToken ct)
    {
        var resources = await (await access.QueryAsync(actor, EvaluationSet.Kind, ct)).AsNoTracking().OrderByDescending(x => x.UpdatedAt).Take(100).ToListAsync(ct);
        var ids = resources.Select(x => x.Id).ToArray();
        var sets = await db.Set<EvaluationSet>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return (await access.DescribeAllAsync(actor, resources, ct)).Select(resource => sets[resource.Id].ToDto(resource)).ToArray();
    }
}
