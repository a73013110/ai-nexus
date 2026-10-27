using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Quality;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Quality.RetrievalEvaluations;

/// <summary>擁有者最近的檢索評測；若某筆所用的知識庫已無法讀取，則略過該筆。</summary>
internal sealed class ListRetrievalEvaluations(AppDbContext db, RetrievalEvaluationService evaluations)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/retrieval-evals", async (ICurrentUser user, ListRetrievalEvaluations handler, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            return TypedResults.Ok(await handler.HandleAsync(user.Id, ct));
        }).Audited(QualityAudit.RetrievalRead);

    public async Task<IReadOnlyList<RetrievalEvaluationDto>> HandleAsync(Guid actor, CancellationToken ct)
    {
        var rows = await (from r in db.Set<RetrievalEvaluation>().AsNoTracking() join j in db.Set<BackgroundJob>().AsNoTracking() on r.JobId equals j.Id where r.OwnerId == actor orderby r.CreatedAt descending select new { r, j }).Take(100).ToListAsync(ct);
        var result = new List<RetrievalEvaluationDto>();
        foreach (var row in rows)
            if ((await evaluations.RequireAccessAsync(actor, RetrievalEvaluationService.Parse<Guid>(row.r.CollectionsJson), ct)).IsSuccess) result.Add(RetrievalEvaluationService.Describe(row.r, row.j));
        return result;
    }
}
