using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Jobs;

// Every persisted checkpoint is fenced by the current lease in the same transaction.
public sealed class JobExecution(NexusDbContext db, BackgroundJob job, Guid lease)
{
    public BackgroundJob Job => job;
    public NexusDbContext Database => db;
    public async Task CheckpointAsync(string stage, int completed, int? total, CancellationToken ct, Func<Task>? afterSave = null, System.Data.IsolationLevel isolation = System.Data.IsolationLevel.ReadCommitted)
    {
        ct.ThrowIfCancellationRequested();
        await using var transaction = await db.Database.BeginTransactionAsync(isolation, ct);
        var changed = await db.Set<BackgroundJob>().Where(x => x.Id == job.Id && x.LeaseToken == lease && x.Status == "running" && !x.CancelRequested)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.Stage, stage).SetProperty(x => x.CompletedUnits, completed).SetProperty(x => x.TotalUnits, total).SetProperty(x => x.UpdatedAt, db.Clock.GetUtcNow()).SetProperty(x => x.LeaseUntil, db.Clock.GetUtcNow().AddSeconds(60)), ct);
        if (changed != 1) throw new OperationCanceledException("Job lease lost or cancellation requested.", ct);
        await db.SaveChangesAsync(ct);
        if (afterSave is not null) await afterSave();
        await transaction.CommitAsync(ct);
    }
}
