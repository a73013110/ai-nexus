using System.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Retrieval;

namespace AiNexus.Features.Quality.RetrievalEvaluations;

public sealed class RetrievalEvaluationHandler(NexusDbContext db, RetrievalEvaluationService evaluations, RetrievalPipeline pipeline) : IBackgroundJobHandler
{
    public string Kind => "retrieval-eval";
    public async Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct)
    {
        var run = await evaluations.RequireAsync(job.OwnerId, job.SubjectId, ct, unchanged: true);
        return run.IsSuccess ? Result.Success : run.Error;
    }
    public async Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct)
    {
        var required = await evaluations.RequireAsync(execution.Job.OwnerId, execution.Job.SubjectId, ct, unchanged: true);
        if (!required.IsSuccess) return required.Error;
        var run = required.Value;
        var cases = RetrievalEvaluationService.Parse<RetrievalEvaluationCase>(run.CasesJson); var collections = RetrievalEvaluationService.Parse<Guid>(run.CollectionsJson);
        var existing = await db.Set<RetrievalEvaluationResult>().AsNoTracking().Where(x => x.RunId == run.Id).Select(x => new { x.CaseIndex, x.Mode }).ToListAsync(ct);
        var completed = existing.Count; var total = cases.Length * RetrievalMetrics.Modes.Count;
        for (var i = 0; i < cases.Length; i++)
            foreach (var mode in RetrievalMetrics.Modes)
            {
                if (existing.Any(x => x.CaseIndex == i && x.Mode == mode)) continue;
                if (await evaluations.RequireAsync(run.OwnerId, run.Id, ct, unchanged: true) is { IsSuccess: false } revoked) return revoked.Error;
                await execution.CheckpointAsync($"第 {i + 1} 題 · {mode}", completed, total, ct);
                var timer = Stopwatch.StartNew(); var result = new RetrievalEvaluationResult { RunId = run.Id, CaseIndex = i, Mode = mode };
                try
                {
                    var searched = await pipeline.SearchAsync(run.OwnerId, new(cases[i].Query, collections), ct, mode: mode == "hybrid+rerank" ? "hybrid" : mode, rerank: mode == "hybrid+rerank");
                    if (!searched.IsSuccess) return searched.Error;
                    var search = searched.Value;
                    var metrics = RetrievalMetrics.Measure(cases[i], search.Hits, run.TopK);
                    result.ActualMode = search.Mode; result.Recall = metrics.Recall; result.ReciprocalRank = metrics.ReciprocalRank; result.Ndcg = metrics.Ndcg; result.Refused = metrics.Refused;
                    result.RewriteMs = search.RewriteMs; result.EmbedMs = search.EmbedMs; result.SearchMs = search.SearchMs; result.RerankMs = search.RerankMs;
                }
                catch (ExternalServiceException error) when (error.Error.Code is "fulltext_unavailable" or "rerank_unavailable") { result.Unavailable = error.Error.Code; result.ActualMode = "unavailable"; }
                result.ElapsedMs = timer.ElapsedMilliseconds;
                if (await evaluations.RequireAsync(run.OwnerId, run.Id, ct, unchanged: true) is { IsSuccess: false } changed) return changed.Error;
                db.Add(result);
                await execution.CheckpointAsync($"完成 {completed + 1} / {total} 次檢索", ++completed, total, ct);
            }
        return Result.Success;
    }
}
