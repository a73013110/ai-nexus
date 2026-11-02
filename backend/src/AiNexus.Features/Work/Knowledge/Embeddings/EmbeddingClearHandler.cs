using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Jobs;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

/// <summary>
/// 分批清除已退役的向量。向量可能有數百萬筆，一次刪除會長時間持鎖並撐大交易記錄，所以每批各自提交、不持有任何鎖，文件索引與其他管理操作都不必等。
/// 中斷或重試都安全：已刪的不會回來，沒刪的下次接著刪。模型在途中恢復使用時停止，已刪的向量由重建補回。
/// </summary>
public sealed class EmbeddingClearHandler(AppDbContext db, EmbeddingLifecycle lifecycle, EmbeddingVectorStore vectors, AccessService access, AuditTrail audit) : IBackgroundJobHandler
{
    public const string JobKind = "embedding-clear";

    public async Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct) => (await ProfileAsync(job, ct)) is { IsSuccess: false } invalid ? invalid.Error : Result.Success;

    /// <summary>取得要清除的向量模型；擁有者仍需具備檢索管理頁的授權，且模型仍是退役狀態。</summary>
    private async Task<Result<EmbeddingProfile>> ProfileAsync(BackgroundJob job, CancellationToken ct)
    {
        if (!await access.HoldsAsync(job.OwnerId, FeatureCatalog.KnowledgeRetrieval, ct)) return AccessControlErrors.FeatureForbidden;
        var profile = await lifecycle.RequireAsync(EmbeddingJobs.Profile(job.SubjectId), ct);
        if (profile.IsSuccess && profile.Value.Status != "retired") return KnowledgeErrors.ProfileNotRetired;
        return profile;
    }

    public async Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct)
    {
        var found = await ProfileAsync(execution.Job, ct);
        if (!found.IsSuccess) return found.Error;
        var profile = found.Value;
        var total = await vectors.Rows(profile).CountAsync(ct);
        var cleared = 0;
        await execution.CheckpointAsync("清除退役向量", cleared, total, ct);
        while (true)
        {
            // 每批都重新檢查：授權被撤銷或模型恢復使用時，不再繼續刪。
            if (await ValidateRetryAsync(execution.Job, ct) is { IsSuccess: false } revoked) return revoked.Error;
            var deleted = await vectors.DeleteRetiredBatchAsync(profile, ct);
            if (deleted == 0) break;
            cleared += deleted;
            await execution.CheckpointAsync("清除退役向量", cleared, total, ct);
        }
        audit.Record(KnowledgeAudit.ProfileCleared, execution.Job.OwnerId, details: new { profileId = profile.Id, vectors = cleared });
        await db.SaveChangesAsync(ct);
        return Result.Success;
    }
}
