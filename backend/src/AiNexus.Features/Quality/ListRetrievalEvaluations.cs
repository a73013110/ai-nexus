using AiNexus.Features.Identity;
using AiNexus.Features.Operations;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Quality;

/// <summary>The owner's latest retrieval evaluations, leaving out any whose collections they can no longer read.</summary>
internal static class ListRetrievalEvaluations
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/retrieval-evals", async (ICurrentUser user, NexusDbContext db, RetrievalEvaluationService evaluations, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(await HandleAsync(user.Id, db, evaluations, ct));
        })
        .Produces<IReadOnlyList<RetrievalEvaluationDto>>();

    private static async Task<IReadOnlyList<RetrievalEvaluationDto>> HandleAsync(Guid actor, NexusDbContext db, RetrievalEvaluationService evaluations, CancellationToken ct)
    {
        var rows = await (from r in db.Set<RetrievalEvaluation>().AsNoTracking() join j in db.Set<BackgroundJob>().AsNoTracking() on r.JobId equals j.Id where r.OwnerId == actor orderby r.CreatedAt descending select new { r, j }).Take(100).ToListAsync(ct);
        var result = new List<RetrievalEvaluationDto>();
        foreach (var row in rows)
        {
            try { await evaluations.RequireAccessAsync(actor, RetrievalEvaluationService.Parse<Guid>(row.r.CollectionsJson), ct); result.Add(RetrievalEvaluationService.Describe(row.r, row.j)); }
            catch (ApiException error) when (error.Status is 403 or 404) { }
        }
        return result;
    }
}
