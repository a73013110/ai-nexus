using AiNexus.BuildingBlocks;
using AiNexus.Modules.Conversations;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Inference;

/// <summary>Expiry is checked again in an atomic UPDATE, so a renewed foreign lease cannot be reclaimed.</summary>
public sealed class RunLeaseRecovery(NexusDbContext db, ConversationService conversations)
{
    public async Task<int> RecoverAsync(DateTimeOffset now, CancellationToken ct)
    {
        var expired = await db.Runs.AsNoTracking()
            .Where(x => x.ActiveOwnerId != null && (x.LeaseExpiresAt == null || x.LeaseExpiresAt <= now))
            .Select(x => x.Id).Take(100).ToArrayAsync(ct);
        var recovered = 0;
        foreach (var id in expired)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var changed = await db.Runs
                .Where(x => x.Id == id && x.ActiveOwnerId != null && (x.LeaseExpiresAt == null || x.LeaseExpiresAt <= now))
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, RunStates.Failed)
                    .SetProperty(x => x.ErrorCode, "executor_lost")
                    .SetProperty(x => x.ActiveOwnerId, (Guid?)null)
                    .SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.FinishedAt, now), ct);
            if (changed == 0) { await transaction.RollbackAsync(ct); continue; }
            var run = await db.Runs.SingleAsync(x => x.Id == id, ct);
            await db.Entry(run).ReloadAsync(ct);
            RunService.AddEvent(db, run, "status");
            await conversations.UpdateAnswerAsync(run, ct);
            db.AuditEvents.Add(new AuditEvent { OwnerId = run.OwnerId, Action = "run.recovered", ResourceId = id, Result = "executor_lost" });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            recovered++;
        }
        return recovered;
    }
}
