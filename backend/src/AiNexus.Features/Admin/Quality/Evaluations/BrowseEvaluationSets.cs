using AiNexus.Data.App;
using AiNexus.Data.App.Quality;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Quality.Evaluations;

/// <summary>使用者可讀取的題組：最近 100 筆，以及單一題組（含題目）。</summary>
internal sealed class BrowseEvaluationSets(AppDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder MapList(RouteGroupBuilder routes) => routes
        .MapGet("/sets", async (ICurrentUser user, BrowseEvaluationSets handler, CancellationToken ct) => TypedResults.Ok(await handler.ListAsync(user.Id, ct)))
        .WithRequestBodyLimit(QualityModule.SetBodyLimit).Audited(QualityAudit.SetRead);

    public static RouteHandlerBuilder MapGet(RouteGroupBuilder routes) => routes
        .MapGet("/sets/{id:guid}", (Guid id, ICurrentUser user, BrowseEvaluationSets handler, CancellationToken ct) => handler.GetAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithRequestBodyLimit(QualityModule.SetBodyLimit).Audited(QualityAudit.SetRead);

    public Task<Result<EvaluationSetDto>> GetAsync(Guid actor, Guid id, CancellationToken ct) => access.LoadSetAsync(db, actor, id, ct);

    public async Task<IReadOnlyList<EvaluationSetDto>> ListAsync(Guid actor, CancellationToken ct)
    {
        var rows = await (await access.QueryAsync(actor, EvaluationSet.Kind, ct)).AsNoTracking()
            .Join(db.Set<EvaluationSet>().AsNoTracking(), resource => resource.Id, set => set.Id, (resource, set) => new { resource, set })
            .OrderByDescending(x => x.resource.UpdatedAt).Take(100).ToListAsync(ct);
        var described = await access.DescribeAllAsync(actor, [.. rows.Select(x => x.resource)], ct);
        return [.. rows.Zip(described, (row, resource) => row.set.ToDto(resource))];
    }
}
