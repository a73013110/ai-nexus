using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;
using System.Text.Json;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Quality.RetrievalEvaluations;

public sealed record RetrievalReportDto(RetrievalEvaluationDto Run, IReadOnlyList<RetrievalSummaryDto> Summary, IReadOnlyList<RetrievalMetricDto> Results, string RefusalDefinition = "無答案題未提供任何來源的比例；不評斷生成回答內容。", string LatencyDefinition = "依 vector、keyword、hybrid、hybrid+rerank 順序執行；共用正常查詢快取，延遲包含快取命中。相鄰片段合併後依相關文件及頁碼評分。");

/// <summary>A retrieval evaluation's metrics and per-mode summary, shown or downloaded. Never cached by the browser.</summary>
internal sealed class GetRetrievalReport(NexusDbContext db, RetrievalEvaluationService evaluations)
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("/retrieval-evals/{id:guid}", async (Guid id, ICurrentUser user, GetRetrievalReport handler, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            return (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult();
        }).Produces<RetrievalReportDto>();
        routes.MapGet("/retrieval-evals/{id:guid}/report", async (Guid id, ICurrentUser user, GetRetrievalReport handler, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            var report = await handler.HandleAsync(user.Id, id, ct);
            return report.IsSuccess
                ? Results.File(JsonSerializer.SerializeToUtf8Bytes(report.Value, RetrievalEvaluationService.Json), "application/json", $"retrieval-{id:N}.json")
                : report.Error.ToProblem();
        }).Produces<RetrievalReportDto>();
    }

    public async Task<Result<RetrievalReportDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var run = await db.Set<RetrievalEvaluation>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor, ct);
        if (run is null) return QualityErrors.RetrievalMissing;
        var collections = RetrievalEvaluationService.Parse<Guid>(run.CollectionsJson);
        await evaluations.RequireAccessAsync(actor, collections, ct);
        var cases = RetrievalEvaluationService.Parse<RetrievalEvaluationCase>(run.CasesJson);
        var job = await db.Set<BackgroundJob>().AsNoTracking().SingleAsync(x => x.Id == run.JobId, ct);
        var rows = await db.Set<RetrievalEvaluationResult>().AsNoTracking().Where(x => x.RunId == id).OrderBy(x => x.CaseIndex).ThenBy(x => x.Mode).ToListAsync(ct);
        var metrics = rows.Select(x => new RetrievalMetricDto(cases[x.CaseIndex].Id, x.Mode, x.ActualMode, x.Unavailable, x.Recall, x.ReciprocalRank, x.Ndcg, x.Refused, x.RewriteMs, x.EmbedMs, x.SearchMs, x.RerankMs, x.ElapsedMs)).ToArray();
        // Access is checked again after reading, so a grant revoked meanwhile releases nothing.
        await evaluations.RequireAccessAsync(actor, collections, ct);
        return new RetrievalReportDto(RetrievalEvaluationService.Describe(run, job), RetrievalMetrics.Summarize(metrics, cases.Length), metrics);
    }
}
