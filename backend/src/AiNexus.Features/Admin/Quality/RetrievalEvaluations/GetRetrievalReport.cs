using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Quality;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Quality.RetrievalEvaluations;

public sealed record RetrievalReportDto(RetrievalEvaluationDto Run, IReadOnlyList<RetrievalSummaryDto> Summary, IReadOnlyList<RetrievalMetricDto> Results, string RefusalDefinition = "無答案題未提供任何來源的比例；不評斷生成回答內容。", string LatencyDefinition = "依 vector、keyword、hybrid、hybrid+rerank 順序執行；共用正常查詢快取，延遲包含快取命中。相鄰片段合併後依相關文件及頁碼評分。");

/// <summary>檢索評測的指標與各模式摘要，供顯示或下載；瀏覽器不得快取。</summary>
internal sealed class GetRetrievalReport(AppDbContext db, RetrievalEvaluationService evaluations)
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("/retrieval-evals/{id:guid}", (Guid id, ICurrentUser user, GetRetrievalReport handler, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            return handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync();
        });
        routes.MapGet("/retrieval-evals/{id:guid}/report", (Guid id, ICurrentUser user, GetRetrievalReport handler, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            return handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync(report =>
                TypedResults.File(JsonSerializer.SerializeToUtf8Bytes(report, RetrievalEvaluationService.Json), "application/json", $"retrieval-{id:N}.json"));
        }).Produces<RetrievalReportDto>();
    }

    public async Task<Result<RetrievalReportDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var run = await db.Set<RetrievalEvaluation>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor, ct);
        if (run is null) return QualityErrors.RetrievalMissing;
        var collections = RetrievalEvaluationService.Parse<Guid>(run.CollectionsJson);
        if (await evaluations.RequireAccessAsync(actor, collections, ct) is { IsSuccess: false } denied) return denied.Error;
        var cases = RetrievalEvaluationService.Parse<RetrievalEvaluationCase>(run.CasesJson);
        var job = await db.Set<BackgroundJob>().AsNoTracking().SingleAsync(x => x.Id == run.JobId, ct);
        var rows = await db.Set<RetrievalEvaluationResult>().AsNoTracking().Where(x => x.RunId == id).OrderBy(x => x.CaseIndex).ThenBy(x => x.Mode).ToListAsync(ct);
        var metrics = rows.Select(x => new RetrievalMetricDto(cases[x.CaseIndex].Id, x.Mode, x.ActualMode, x.Unavailable, x.Recall, x.ReciprocalRank, x.Ndcg, x.Refused, x.RewriteMs, x.EmbedMs, x.SearchMs, x.RerankMs, x.ElapsedMs)).ToArray();
        // 讀取後再檢查一次權限：讀取期間若權限已被撤銷，就不釋出任何資料。
        if (await evaluations.RequireAccessAsync(actor, collections, ct) is { IsSuccess: false } revoked) return revoked.Error;
        return new RetrievalReportDto(RetrievalEvaluationService.Describe(run, job), RetrievalMetrics.Summarize(metrics, cases.Length), metrics);
    }
}
