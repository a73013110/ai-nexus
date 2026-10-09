using System.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Quality.Evaluations;

// Lexical checks are explicit diagnostics, not a claim of semantic correctness.
public static class EvaluationMetrics
{
    public static (int Matches, int Total, int Forbidden) Measure(string answer, EvaluationCase test)
    {
        var required = test.RequiredTerms ?? []; var forbidden = test.ForbiddenTerms ?? [];
        return (required.Count(x => answer.Contains(x.Trim(), StringComparison.OrdinalIgnoreCase)), required.Count, forbidden.Count(x => answer.Contains(x.Trim(), StringComparison.OrdinalIgnoreCase)));
    }
}

public sealed class EvaluationHandler(NexusDbContext db, ResourceAccess access, AccessService features, ModelTaskService model, ModelPresentation presentation, ModelCatalog catalog, IOptions<InferenceOptions> inference) : IBackgroundJobHandler
{
    public string Kind => "evaluation";
    private async Task<EvaluationRun> RequireAsync(BackgroundJob job, CancellationToken ct)
    {
        if (!(await features.ForUserAsync(job.OwnerId, ct)).Features.Any(x => x.Id == "quality")) throw new ApiException(403, "evaluation_access_revoked", "你的品質評測功能已停用。");
        var run = await db.Set<EvaluationRun>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == job.SubjectId && x.OwnerId == job.OwnerId, ct) ?? throw new ApiException(404, "evaluation_run_missing", "找不到原始評測。");
        (await access.RequireAsync(job.OwnerId, run.SetId, "evaluation", ct)).OrThrow(); return run;
    }
    public async Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct)
    {
        var run = await RequireAsync(job, ct);
        foreach (var variant in EvaluationJson.Parse<EvaluationVariant>(run.VariantsJson))
            ModelTaskConfiguration.Require(await catalog.RequireAsync(presentation.PublicId(variant.ModelId), ct), inference.Value, variant.Configuration?.Fingerprint);
        return Result.Success;
    }
    public async Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct)
    {
        var run = await RequireAsync(execution.Job, ct); var cases = EvaluationJson.Parse<EvaluationCase>(run.CasesJson); var variants = EvaluationJson.Parse<EvaluationVariant>(run.VariantsJson);
        var existing = await db.Set<EvaluationResult>().AsNoTracking().Where(x => x.RunId == run.Id).Select(x => new { x.CaseIndex, x.VariantIndex }).ToListAsync(ct);
        var completed = existing.Count; var total = cases.Length * variants.Length;
        for (var c = 0; c < cases.Length; c++)
            for (var v = 0; v < variants.Length; v++)
            {
                if (existing.Any(x => x.CaseIndex == c && x.VariantIndex == v)) continue;
                await RequireAsync(execution.Job, ct);
                await execution.CheckpointAsync($"第 {c + 1} 題 · {variants[v].Label}", completed, total, ct);
                var timer = Stopwatch.StartNew();
                // The reference answer and check terms are withheld from the model.
                var answer = await model.GenerateAsync(run.OwnerId, "evaluation", cases[c].Question, variants[v].Instruction, ct, presentation.PublicId(variants[v].ModelId), expectedConfiguration: variants[v].Configuration?.Fingerprint);
                await RequireAsync(execution.Job, ct); var metrics = EvaluationMetrics.Measure(answer.Text, cases[c]);
                db.Add(new EvaluationResult { RunId = run.Id, CaseIndex = c, VariantIndex = v, Output = answer.Text, Truncated = answer.Truncated, RequiredMatches = metrics.Matches, RequiredTotal = metrics.Total, ForbiddenMatches = metrics.Forbidden, ElapsedMs = timer.ElapsedMilliseconds, InputTokens = answer.InputTokens, OutputTokens = answer.OutputTokens });
                await execution.CheckpointAsync($"完成 {completed + 1} / {total} 次回答", ++completed, total, ct);
            }
        return Result.Success;
    }
}
