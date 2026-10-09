using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Quality.Evaluations;

/// <summary>A set's latest 100 runs, and one run with its frozen cases, variants and results. Both require read access to the set.</summary>
internal sealed class BrowseEvaluationRuns(NexusDbContext db, ResourceAccess access, ModelPresentation presentation)
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("/sets/{id:guid}/runs", async (Guid id, ICurrentUser user, BrowseEvaluationRuns handler, CancellationToken ct) => Results.Ok(await handler.ListAsync(user.Id, id, ct)))
            .Produces<IReadOnlyList<EvaluationRunDto>>().WithRequestBodyLimit(QualityModule.SetBodyLimit);
        routes.MapGet("/runs/{id:guid}", async (Guid id, ICurrentUser user, BrowseEvaluationRuns handler, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            return (await handler.DetailAsync(user.Id, id, ct)).ToHttpResult();
        }).Produces<EvaluationDetailDto>();
    }

    public async Task<IReadOnlyList<EvaluationRunDto>> ListAsync(Guid actor, Guid setId, CancellationToken ct)
    {
        await access.RequireAsync(actor, setId, EvaluationSet.Kind, ct);
        var rows = await (from r in db.Set<EvaluationRun>() join j in db.Set<BackgroundJob>() on r.JobId equals j.Id where r.SetId == setId orderby r.CreatedAt descending select new { r, j }).Take(100).ToListAsync(ct);
        return rows.Select(x => x.r.ToDto(x.j, actor)).ToList();
    }

    public async Task<Result<EvaluationDetailDto>> DetailAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var run = await db.Set<EvaluationRun>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (run is null) return QualityErrors.ItemMissing;
        var resource = await access.RequireAsync(actor, run.SetId, EvaluationSet.Kind, ct);
        var job = await db.Set<BackgroundJob>().AsNoTracking().SingleAsync(x => x.Id == run.JobId, ct);
        var variants = EvaluationJson.Parse<EvaluationVariant>(run.VariantsJson).Select(x => x with { ModelId = presentation.PublicId(x.ModelId), ModelDisplayName = presentation.DisplayName(x.ModelId) }).ToArray();
        var results = await db.Set<EvaluationResult>().AsNoTracking().Where(x => x.RunId == id).OrderBy(x => x.CaseIndex).ThenBy(x => x.VariantIndex).ToListAsync(ct);
        return new EvaluationDetailDto(run.ToDto(job, actor), EvaluationJson.Parse<EvaluationCase>(run.CasesJson), variants,
            results.Select(x => new EvaluationResultDto(x.CaseIndex, x.VariantIndex, x.Output, x.Truncated, x.RequiredMatches, x.RequiredTotal, x.ForbiddenMatches, x.ElapsedMs, x.InputTokens, x.OutputTokens, x.ReviewScore, x.ReviewNote)).ToArray(),
            await access.CanEditAsync(actor, resource, ct));
    }
}

public sealed record EvaluationResultDto(int CaseIndex, int VariantIndex, string Output, bool Truncated, int RequiredMatches, int RequiredTotal, int ForbiddenMatches, long ElapsedMs, long? InputTokens, long? OutputTokens, int? ReviewScore, string ReviewNote);

public sealed record EvaluationDetailDto(EvaluationRunDto Run, IReadOnlyList<EvaluationCase> Cases, IReadOnlyList<EvaluationVariant> Variants, IReadOnlyList<EvaluationResultDto> Results, bool CanReview);
