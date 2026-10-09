using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Conversations;
using AiNexus.Platform.Diagnostics;
using AiNexus.Features.Billing;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Inference;

namespace AiNexus.Features.Chat;

/// <summary>
/// Expiry is checked again in an atomic UPDATE, so a renewed foreign lease cannot be reclaimed. Each run is recovered
/// under its conversation's generation lock, so a local flush or cancellation of the same run cannot interleave.
/// </summary>
public sealed class RunLeaseRecovery(NexusDbContext db, GenerationScheduler scheduler, RunSignals signals, ConversationService conversations, BillingService billing, Issues issues, ILogger<RunLeaseRecovery> logger, AiNexus.Features.Notifications.NotificationService notifications)
{
    public async Task<int> RecoverAsync(DateTimeOffset now, CancellationToken ct)
    {
        var expired = await db.Runs.AsNoTracking()
            .Where(x => x.ActiveOwnerId != null && (x.LeaseExpiresAt == null || x.LeaseExpiresAt <= now))
            .OrderBy(x => x.LeaseExpiresAt).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.ConversationId }).Take(100).ToArrayAsync(ct);
        var recovered = 0;
        foreach (var (id, conversation) in expired.Select(x => (x.Id, x.ConversationId)))
        {
            using var conversationLock = await scheduler.LockConversationAsync(conversation, ct);
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
            using var activity = DiagnosticTrace.Start("generation.recover", run.TraceId, run.ParentSpanId);
            activity.SetTag("operation.id", run.Id.ToString());
            using var logging = logger.BeginScope(new Dictionary<string, object?> { ["RunId"] = run.Id, ["OperationId"] = run.Id, ["UserId"] = run.OwnerId, ["RequestId"] = null });
            run.IssueCode = issues.Report(new ApiException(503, "executor_lost", ""), "executor_lost");
            if (run.StartedAt is null) run.ReservedTokens = 0;
            RunTiming.Finish(run, now);
            await billing.FinishAsync(run.Id, run.Status, ct);
            RunService.AddEvent(db, run, "status");
            await conversations.UpdateAnswerAsync(run, ct);
            db.AuditEvents.Add(new AuditEvent { OwnerId = run.OwnerId, Action = "run.recovered", ResourceId = id, Result = "executor_lost", IssueCode = run.IssueCode });
            await notifications.PublishAsync(run.OwnerId, "run:" + run.Id, "conversation.failed", "error", "AI 回答未完成", Issues.Message(run.IssueCode), "conversation", run.ConversationId, ct, run.IssueCode);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            signals.Notify(id);
            recovered++;
        }
        return recovered;
    }
}
