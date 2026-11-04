using AiNexus.Data.App;
using AiNexus.Data.App.Quality;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Quality.Evaluations;

public sealed record EvaluationResultDto(int CaseIndex, int VariantIndex, string Output, bool Truncated, int RequiredMatches, int RequiredTotal, int ForbiddenMatches, long ElapsedMs, long? InputTokens, long? OutputTokens, int? ReviewScore, string ReviewNote);

public sealed record EvaluationDetailDto(EvaluationRunDto Run, IReadOnlyList<EvaluationCase> Cases, IReadOnlyList<EvaluationVariant> Variants, IReadOnlyList<EvaluationResultDto> Results, bool CanReview);

/// <summary>某題組最近 100 筆執行紀錄；單筆執行另含凍結的題目、變體與結果。兩者都需要該題組的讀取權限。</summary>
internal sealed class BrowseEvaluationRuns(AppDbContext db, ResourceAccess access, ModelPresentation presentation) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes)
    {
        routes.MapGet("/sets/{id:guid}/runs", (Guid id, ICurrentUser user, BrowseEvaluationRuns handler, CancellationToken ct) =>
            handler.ListAsync(user.Id, id, ct))
            .WithRequestBodyLimit(QualityModule.SetBodyLimit).Audited(QualityAudit.RunRead);
        routes.MapGet("/runs/{id:guid}", (Guid id, ICurrentUser user, BrowseEvaluationRuns handler, CancellationToken ct) =>
            handler.DetailAsync(user.Id, id, ct))
            .Audited(QualityAudit.RunRead);
    }

    public async Task<Result<EvaluationRunDto[]>> ListAsync(Guid actor, Guid setId, CancellationToken ct)
    {
        if (await access.RequireAsync(actor, setId, EvaluationSet.Kind, ct) is { IsSuccess: false } denied) return denied.Error;
        var rows = await (from r in db.EvaluationRuns join j in db.BackgroundJobs on r.JobId equals j.Id where r.SetId == setId orderby r.CreatedAt descending select new { r, j }).Take(100).ToListAsync(ct);
        return rows.Select(x => x.r.ToDto(x.j, actor)).ToArray();
    }

    public async Task<Result<EvaluationDetailDto>> DetailAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var run = await db.EvaluationRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (run is null) return QualityErrors.ItemMissing;
        var allowed = await access.RequireAsync(actor, run.SetId, EvaluationSet.Kind, ct);
        if (!allowed.IsSuccess) return allowed.Error;
        var resource = allowed.Value;
        var job = await db.BackgroundJobs.AsNoTracking().SingleAsync(x => x.Id == run.JobId, ct);
        var variants = EvaluationJson.Parse<EvaluationVariant>(run.VariantsJson).Select(x => x with { ModelId = presentation.PublicId(x.ModelId), ModelDisplayName = presentation.DisplayName(x.ModelId) }).ToArray();
        var results = await db.EvaluationResults.AsNoTracking().Where(x => x.RunId == id).OrderBy(x => x.CaseIndex).ThenBy(x => x.VariantIndex).ToListAsync(ct);
        return new EvaluationDetailDto(run.ToDto(job, actor), EvaluationJson.Parse<EvaluationCase>(run.CasesJson), variants,
            results.Select(x => new EvaluationResultDto(x.CaseIndex, x.VariantIndex, x.Output, x.Truncated, x.RequiredMatches, x.RequiredTotal, x.ForbiddenMatches, x.ElapsedMs, x.InputTokens, x.OutputTokens, x.ReviewScore, x.ReviewNote)).ToArray(),
            await access.CanEditAsync(actor, resource, ct));
    }
}
