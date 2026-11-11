using AiNexus.Data.App;
using AiNexus.Platform.Data;
using AiNexus.Platform.Scheduling;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Inference.ModelTasks;

/// <summary>
/// 刪除超過 <see cref="InferenceOptions.InvocationRetentionDays"/> 的模型工作呼叫紀錄（<c>inference.ModelInvocations</c>）。
/// 這張表不只算每日配額：用量與花費報表也讀它（<c>inference.udf_UsageEntries</c>、<c>billing.usp_GetSpendReport</c>），
/// 所以保存期不能短於報表查得到的最長期間。費用另記在 <c>billing.ModelCharges</c>，不受影響。
/// 由舊到新分批刪除（<see cref="Batches"/>，走 <c>IX_ModelInvocations_CreatedAt</c>）。
/// </summary>
public sealed class ModelInvocationRetentionTask(AppDbContext db, IOptions<InferenceOptions> options, TimeProvider clock) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "inference.invocation-retention", "模型呼叫紀錄清理", "模型", "刪除超過保存期的模型工作呼叫紀錄；費用紀錄不受影響。");

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
        => TaskOutcome.Count(await DeleteExpiredAsync(ct), total => $"刪除 {total:N0} 筆紀錄");

    /// <summary>分批刪除超過保存期的呼叫紀錄，回傳刪除的總筆數。</summary>
    public Task<int> DeleteExpiredAsync(CancellationToken ct)
    {
        var before = clock.GetUtcNow().AddDays(-options.Value.InvocationRetentionDays);
        return db.ModelInvocations.Where(x => x.CreatedAt < before).OrderBy(x => x.CreatedAt).DeleteInBatchesAsync(ct);
    }
}
