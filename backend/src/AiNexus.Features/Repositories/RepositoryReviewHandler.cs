using System.Diagnostics;
using System.Text;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Repositories;

/// <summary>Runs a frozen review snapshot. Every model call re-checks access and the source, and results are checkpointed per ordinal.</summary>
public sealed class RepositoryReviewHandler(NexusDbContext db, RepositoryReviewService reviews, AccessService features, ModelTaskService model,
    ModelCatalog catalog, ModelPresentation presentation, IOptions<InferenceOptions> inference) : IBackgroundJobHandler
{
    public string Kind => "repository-review";
    private async Task<RepositoryReview> RequireAsync(BackgroundJob job, CancellationToken ct)
    {
        if (!(await features.ForUserAsync(job.OwnerId, ct)).Features.Any(x => x.Id == "repositories")) throw new ApiException(403, "review_access_revoked", "你的程式庫功能已停用。");
        var row = await reviews.OwnedAsync(job.OwnerId, job.SubjectId, ct);
        await reviews.RequireSourceAsync(job.OwnerId, row, ct); return row;
    }
    private async Task<RepositoryReview> RequireCurrentAsync(BackgroundJob job, CancellationToken ct)
    {
        var row = await RequireAsync(job, ct);
        ModelTaskConfiguration.Require((await catalog.RequireAsync(presentation.PublicId(row.ModelId), ct)).OrThrow(), inference.Value, row.ConfigurationFingerprint).OrThrow();
        return row;
    }
    public async Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct) { await RequireCurrentAsync(job, ct); return Result.Success; }
    public async Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct)
    {
        var row = await RequireCurrentAsync(execution.Job, ct); var snapshot = RepositoryReviewService.Snapshot(row); var slices = snapshot.Slices;
        var results = await db.Set<RepositoryReviewResult>().AsNoTracking().Where(x => x.ReviewId == row.Id).ToDictionaryAsync(x => x.Ordinal, ct);
        var modern = snapshot.Version >= 2;
        if (modern && (snapshot.Direct || slices.All(x => x.Binary)))
        {
            if (results.ContainsKey(RepositoryReviewPlan.ReportOrdinal)) return Result.Success;
            await execution.CheckpointAsync("正在分析整體變更", 0, 1, ct);
            var binary = slices.All(x => x.Binary);
            await GenerateAsync(execution, row, snapshot, RepositoryReviewPlan.ReportOrdinal, RepositoryReviewPlan.Source(slices), snapshot.ReportInstruction!,
                "untrusted_diff", "整體報告已完成", 1, 1, ct, maxOutputTokens: snapshot.ReportOutputTokens,
                answer: binary ? new("此變更只有二進位內容，需人工確認原檔。" + (snapshot.Version < 3 ? "\n" + RepositoryReviewPlan.Source(slices) : ""), false, null, null) : null);
            return Result.Success;
        }
        var completed = results.Keys.Count(x => x >= 0 && x < slices.Length);
        var total = slices.Length + (modern ? 1 : 0);
        for (var i = 0; i < slices.Length; i++)
        {
            if (results.ContainsKey(i)) continue;
            await execution.CheckpointAsync($"檢閱區段 {i + 1} / {slices.Length}", completed, total, ct);
            var slice = slices[i];
            results[i] = await GenerateAsync(execution, row, snapshot, i, $"區段：{slice.Label}\n{slice.Diff}", snapshot.Instruction,
                "untrusted_diff", $"已分析 {++completed} / {slices.Length} 區段", completed, total, ct,
                maxOutputTokens: modern ? snapshot.AnalysisOutputTokens : null,
                answer: slice.Binary ? new("二進位變更未送交文字模型檢閱，請人工確認原檔。", false, null, null) : null);
        }
        if (!modern || results.ContainsKey(RepositoryReviewPlan.ReportOrdinal)) return Result.Success;

        // Intermediate reductions have deterministic ordinals after the source slices, so retries reuse their checkpoints too.
        var evidence = slices.Select((x, i) => $"來源區段 {i + 1}：{x.Label}\n{results[i].Output}" +
            (results[i].Truncated ? "\n注意：此區段分析已達輸出上限，檢閱不完整。" : "")).ToArray();
        var batches = RepositoryReviewPlan.Batches(evidence, snapshot.InputBudget);
        var ordinal = slices.Length;
        var truncated = results.Values.Any(x => x.Truncated);
        for (var level = 1; batches.Length > 1; level++)
        {
            var reduced = new List<string>();
            for (var i = 0; i < batches.Length; i++, ordinal++)
            {
                if (!results.TryGetValue(ordinal, out var result))
                {
                    await execution.CheckpointAsync($"正在彙整整體報告 · 第 {level} 輪 {i + 1} / {batches.Length}", completed, total, ct);
                    result = await GenerateAsync(execution, row, snapshot, ordinal, batches[i], snapshot.ReductionInstruction!, "untrusted_analysis",
                        "正在彙整整體報告", completed, total, ct, maxOutputTokens: Math.Clamp(snapshot.InputBudget / 12, 32, 384), byteLimit: snapshot.InputBudget / 3);
                    results[ordinal] = result;
                }
                truncated |= result.Truncated; reduced.Add(result.Output);
            }
            batches = RepositoryReviewPlan.Batches(reduced, snapshot.InputBudget);
        }
        await execution.CheckpointAsync("正在產生整體報告", completed, total, ct);
        await GenerateAsync(execution, row, snapshot, RepositoryReviewPlan.ReportOrdinal, batches.Single(), snapshot.ReportInstruction!, "untrusted_analysis",
            "整體報告已完成", total, total, ct, maxOutputTokens: snapshot.ReportOutputTokens, truncated: truncated);
        return Result.Success;
    }

    private async Task<RepositoryReviewResult> GenerateAsync(JobExecution execution, RepositoryReview row, ReviewSnapshot snapshot, int ordinal, string text, string instruction,
        string sourceTag, string stage, int completed, int total, CancellationToken ct, int? maxOutputTokens = null, bool truncated = false,
        int? byteLimit = null, ModelTaskResult? answer = null)
    {
        await RequireAsync(execution.Job, ct);
        var timer = Stopwatch.StartNew();
        var prompt = RepositoryReviewPlan.Context(row.Repository, row.Commit, row.BaseCommit, row.Note) + $"<{sourceTag}>\n{text}\n</{sourceTag}>";
        var brief = answer is null && ordinal == RepositoryReviewPlan.ReportOrdinal && snapshot.Version >= 3;
        if (brief) prompt += "\n請依系統指定的 JSON 格式回覆；說明只用繁體中文，合計最多 350 字。";
        answer ??= await GenerateModelAsync(prompt);
        if (brief)
        {
            if (answer.Truncated || !RepositoryReviewBrief.TryRender(answer.Text, snapshot.Purpose, out _))
            {
                await execution.CheckpointAsync("正在整理精簡結果", completed, total, ct);
                await RequireAsync(execution.Job, ct);
                var correction = await GenerateModelAsync(prompt + "\n上一份輸出未通過完整性、繁體中文或長度檢查。請重新生成更短的完整 JSON：結論 40 字、變更與問題各至多 2 項，每項 40 字，不貼程式碼。不要解釋格式。請務必閉合 JSON。");
                answer = correction with { InputTokens = Sum(answer.InputTokens, correction.InputTokens), OutputTokens = Sum(answer.OutputTokens, correction.OutputTokens) };
            }
            if (answer.Truncated || !RepositoryReviewBrief.TryRender(answer.Text, snapshot.Purpose, out var output))
                throw new ApiException(502, "review_brief_invalid", "模型未傳回完整的繁體中文精簡結果，已保留完成的分析。請重試或改用其他模型。");
            answer = answer with { Text = output };
        }
        if (byteLimit is not null && Encoding.UTF8.GetByteCount(answer.Text) > byteLimit)
            throw new ApiException(502, "review_summary_too_long", "模型未能將分析彙整到上下文預算內，已保留完成區段。請重試，或縮小範圍、改用其他模型建立 review。");
        await RequireAsync(execution.Job, ct);
        var result = new RepositoryReviewResult { ReviewId = row.Id, Ordinal = ordinal, Output = answer.Text, Truncated = truncated || answer.Truncated,
            InputTokens = answer.InputTokens, OutputTokens = answer.OutputTokens, ElapsedMs = timer.ElapsedMilliseconds };
        db.Add(result);
        await execution.CheckpointAsync(stage, completed, total, ct);
        return result;

        async Task<ModelTaskResult> GenerateModelAsync(string source) => (await model.GenerateAsync(row.OwnerId, Kind, source,
            instruction, ct, presentation.PublicId(row.ModelId), expectedConfiguration: row.ConfigurationFingerprint, maxOutputTokens: maxOutputTokens)).OrThrow();
        static long? Sum(long? first, long? second) => first is null || second is null ? null : first + second;
    }
}
