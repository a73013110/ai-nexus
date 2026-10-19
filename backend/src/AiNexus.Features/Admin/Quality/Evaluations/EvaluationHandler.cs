using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Quality;
using System.Diagnostics;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.ModelTasks;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Work.Jobs;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Quality.Evaluations;

// 詞彙比對只是明確的診斷指標，不代表回答語意正確。
public static class EvaluationMetrics
{
    public static (int Matches, int Total, int Forbidden) Measure(string answer, EvaluationCase test)
    {
        var required = test.RequiredTerms ?? []; var forbidden = test.ForbiddenTerms ?? [];
        return (required.Count(x => answer.Contains(x.Trim(), StringComparison.OrdinalIgnoreCase)), required.Count, forbidden.Count(x => answer.Contains(x.Trim(), StringComparison.OrdinalIgnoreCase)));
    }
}

public sealed class EvaluationHandler(AppDbContext db, ResourceAccess access, AccessService features, ModelTaskService model, ModelPresentation presentation, ModelCatalog catalog, IOptions<InferenceOptions> inference) : IBackgroundJobHandler
{
    public const string JobKind = "evaluation";

    public string Kind => JobKind;
    private async Task<Result<EvaluationRun>> RequireAsync(BackgroundJob job, CancellationToken ct)
    {
        if (!await features.HoldsAsync(job.OwnerId, FeatureCatalog.Quality, ct)) return AccessControlErrors.FeatureForbidden;
        var run = await db.Set<EvaluationRun>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == job.SubjectId && x.OwnerId == job.OwnerId, ct);
        if (run is null) return QualityErrors.RunMissing;
        if (await access.RequireAsync(job.OwnerId, run.SetId, EvaluationSet.Kind, ct) is { IsSuccess: false } denied) return denied.Error;
        return run;
    }
    public async Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct)
    {
        var run = await RequireAsync(job, ct);
        if (!run.IsSuccess) return run.Error;
        foreach (var variant in EvaluationJson.Parse<EvaluationVariant>(run.Value.VariantsJson))
        {
            var model = await catalog.RequireAsync(presentation.PublicId(variant.ModelId), ct);
            if (!model.IsSuccess) return model.Error;
            if (ModelTaskConfiguration.Require(model.Value, inference.Value, variant.Configuration?.Fingerprint) is { IsSuccess: false } changed) return changed.Error;
        }
        return Result.Success;
    }
    public async Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct)
    {
        var required = await RequireAsync(execution.Job, ct);
        if (!required.IsSuccess) return required.Error;
        var run = required.Value; var cases = EvaluationJson.Parse<EvaluationCase>(run.CasesJson); var variants = EvaluationJson.Parse<EvaluationVariant>(run.VariantsJson);
        var done = (await db.Set<EvaluationResult>().AsNoTracking().Where(x => x.RunId == run.Id).Select(x => new { x.CaseIndex, x.VariantIndex }).ToListAsync(ct))
            .Select(x => (x.CaseIndex, x.VariantIndex)).ToHashSet();
        var completed = done.Count; var total = cases.Length * variants.Length;
        for (var c = 0; c < cases.Length; c++)
            for (var v = 0; v < variants.Length; v++)
            {
                if (done.Contains((c, v))) continue;
                if (await RequireAsync(execution.Job, ct) is { IsSuccess: false } revoked) return revoked.Error;
                await execution.CheckpointAsync($"第 {c + 1} 題 · {variants[v].Label}", completed, total, ct);
                var timer = Stopwatch.StartNew();
                // 參考答案與檢查詞不可送進模型，否則模型可能照抄而使評測失真。
                var generated = await model.GenerateAsync(run.OwnerId, "evaluation", cases[c].Question, variants[v].Instruction, ct, presentation.PublicId(variants[v].ModelId), expectedConfiguration: variants[v].Configuration?.Fingerprint);
                if (!generated.IsSuccess) return generated.Error;
                var answer = generated.Value;
                if (await RequireAsync(execution.Job, ct) is { IsSuccess: false } stopped) return stopped.Error;
                var metrics = EvaluationMetrics.Measure(answer.Text, cases[c]);
                db.Add(new EvaluationResult { RunId = run.Id, CaseIndex = c, VariantIndex = v, Output = answer.Text, Truncated = answer.Truncated, RequiredMatches = metrics.Matches, RequiredTotal = metrics.Total, ForbiddenMatches = metrics.Forbidden, ElapsedMs = timer.ElapsedMilliseconds, InputTokens = answer.InputTokens, OutputTokens = answer.OutputTokens });
                await execution.CheckpointAsync($"完成 {completed + 1} / {total} 次回答", ++completed, total, ct);
            }
        return Result.Success;
    }
}
