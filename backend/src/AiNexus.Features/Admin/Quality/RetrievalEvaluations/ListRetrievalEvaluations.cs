using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Quality.RetrievalEvaluations;

/// <summary>擁有者最近的檢索評測；若某筆所用的知識庫已無法讀取，則略過該筆。</summary>
internal sealed class ListRetrievalEvaluations(AppDbContext db, RetrievalEvaluationService evaluations) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/retrieval-evals", (ICurrentUser user, ListRetrievalEvaluations handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, ct))
        .Audited(QualityAudit.RetrievalRead);

    public async Task<IReadOnlyList<RetrievalEvaluationDto>> HandleAsync(Guid actor, CancellationToken ct)
    {
        var rows = await (from r in db.RetrievalEvaluations.AsNoTracking() join j in db.BackgroundJobs.AsNoTracking() on r.JobId equals j.Id where r.OwnerId == actor orderby r.CreatedAt descending select new { r, j }).Take(100).ToListAsync(ct);
        var result = new List<RetrievalEvaluationDto>();
        foreach (var row in rows)
            if ((await evaluations.RequireAccessAsync(actor, RetrievalEvaluationService.Parse<Guid>(row.r.CollectionsJson), ct)).IsSuccess) result.Add(RetrievalEvaluationService.Describe(row.r, row.j));
        return result;
    }
}
