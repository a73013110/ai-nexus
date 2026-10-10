using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Quality.RetrievalEvaluations;

/// <summary>The owner's latest retrieval evaluations, leaving out any whose collections they can no longer read.</summary>
internal sealed class ListRetrievalEvaluations(NexusDbContext db, RetrievalEvaluationService evaluations)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/retrieval-evals", async (ICurrentUser user, ListRetrievalEvaluations handler, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            return TypedResults.Ok(await handler.HandleAsync(user.Id, ct));
        });

    public async Task<IReadOnlyList<RetrievalEvaluationDto>> HandleAsync(Guid actor, CancellationToken ct)
    {
        var rows = await (from r in db.Set<RetrievalEvaluation>().AsNoTracking() join j in db.Set<BackgroundJob>().AsNoTracking() on r.JobId equals j.Id where r.OwnerId == actor orderby r.CreatedAt descending select new { r, j }).Take(100).ToListAsync(ct);
        var result = new List<RetrievalEvaluationDto>();
        foreach (var row in rows)
            if ((await evaluations.RequireAccessAsync(actor, RetrievalEvaluationService.Parse<Guid>(row.r.CollectionsJson), ct)).IsSuccess) result.Add(RetrievalEvaluationService.Describe(row.r, row.j));
        return result;
    }
}
