using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Jobs;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Jobs;

// 每個已保存的檢查點都與當前租約（fencing token）在同一交易中比對；租約已失效或已要求取消時拒絕寫入。
public sealed class JobExecution(AppDbContext db, BackgroundJob job, Guid lease)
{
    public BackgroundJob Job => job;
    public AppDbContext Database => db;
    public async Task CheckpointAsync(
        string stage,
        int completed,
        int? total,
        CancellationToken ct,
        Func<Task>? afterSave = null,
        System.Data.IsolationLevel isolation = System.Data.IsolationLevel.ReadCommitted)
    {
        ct.ThrowIfCancellationRequested();
        await using var transaction = await db.Database.BeginTransactionAsync(isolation, ct);
        var changed = await db.Set<BackgroundJob>()
            .Where(x => x.Id == job.Id && x.LeaseToken == lease && x.Status == RunStates.Running && !x.CancelRequested)
            .ExecuteUpdateAsync(p => p
                .SetProperty(x => x.Stage, stage)
                .SetProperty(x => x.CompletedUnits, completed)
                .SetProperty(x => x.TotalUnits, total)
                .SetProperty(x => x.UpdatedAt, db.Clock.GetUtcNow())
                .SetProperty(x => x.LeaseUntil, db.Clock.GetUtcNow().AddSeconds(JobService.LeaseSeconds)), ct);
        // 租約已被其他 worker 接手，或使用者剛要求取消：這次檢查點不能寫入，直接中止執行。
        if (changed != 1) throw new OperationCanceledException("Job lease lost or cancellation requested.", ct);
        await db.SaveChangesAsync(ct);
        if (afterSave is not null) await afterSave();
        await transaction.CommitAsync(ct);
    }
}
