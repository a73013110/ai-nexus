using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Data.App.Inference;
using AiNexus.Features.Billing;
using AiNexus.Features.Conversations;
using AiNexus.Features.Inference.Runs;
using AiNexus.Features.Notifications;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Chat.Runs;

/// <summary>
/// 生成租約逾期的復原服務。伺服器程序崩潰、斷線或重啟後，由其他健康程序接手，
/// 把懸掛的孤兒生成標記為失敗。租約檢查寫在同一條條件式 UPDATE 中，原執行者若剛好續約會回傳 0 行而略過；
/// 每個生成仍在所屬對話鎖下結算，避免與本機沖刷或取消互相競態。
/// </summary>
public sealed class RunLeaseRecovery(
    AppDbContext db,
    GenerationScheduler scheduler,
    RunSignals signals,
    ConversationService conversations,
    BillingService billing,
    Issues issues,
    ILogger<RunLeaseRecovery> logger,
    NotificationService notifications)
{
    /// <summary>
    /// 掃描並復原所有租約已超時的活躍生成記錄。
    /// </summary>
    public async Task<int> RecoverAsync(DateTimeOffset now, CancellationToken ct)
    {
        var expired = await db.Runs.AsNoTracking()
            .Where(x => x.ActiveOwnerId != null && (x.LeaseExpiresAt == null || x.LeaseExpiresAt <= now))
            .OrderBy(x => x.LeaseExpiresAt).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.ConversationId })
            .Take(100)
            .ToArrayAsync(ct);

        var recovered = 0;
        foreach (var (id, conversation) in expired.Select(x => (x.Id, x.ConversationId)))
        {
            using var conversationLock = await scheduler.LockConversationAsync(conversation, ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // 條件式 UPDATE：若原執行者剛好續約，影響行數為 0，直接略過，不會誤判為失敗
            var changed = await db.Runs
                .Where(x => x.Id == id && x.ActiveOwnerId != null && (x.LeaseExpiresAt == null || x.LeaseExpiresAt <= now))
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, RunStates.Failed)
                    .SetProperty(x => x.ErrorCode, "executor_lost")
                    .SetProperty(x => x.ActiveOwnerId, (Guid?)null)
                    .SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.FinishedAt, now), ct);

            if (changed == 0)
            {
                await transaction.RollbackAsync(ct);
                continue;
            }

            var run = await db.Runs.SingleAsync(x => x.Id == id, ct);
            await db.Entry(run).ReloadAsync(ct);

            using var activity = DiagnosticTrace.Start("generation.recover", run.TraceId, run.ParentSpanId);
            activity.SetTag("operation.id", run.Id.ToString());

            using var logging = logger.BeginScope(new Dictionary<string, object?>
            {
                ["RunId"] = run.Id,
                ["OperationId"] = run.Id,
                ["UserId"] = run.OwnerId,
                ["RequestId"] = null
            });

            run.IssueCode = issues.Report(Error.Unavailable("executor_lost"));
            if (run.StartedAt is null)
            {
                run.ReservedTokens = 0;
            }

            RunTiming.Finish(run, now);
            await billing.FinishAsync(run.Id, run.Status, ct);

            RunService.AddEvent(db, run, "status");
            await conversations.UpdateAnswerAsync(run, ct);

            db.AuditEvents.Add(new AuditEvent
            {
                OwnerId = run.OwnerId,
                Action = "run.recovered",
                ResourceId = id,
                Result = "executor_lost",
                IssueCode = run.IssueCode
            });

            await notifications.PublishAsync(
                run.OwnerId,
                "run:" + run.Id,
                "conversation.failed",
                "error",
                "AI 回答未完成",
                Issues.Message(run.IssueCode),
                "conversation",
                run.ConversationId,
                ct,
                run.IssueCode);

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            signals.Notify(id);
            recovered++;
        }

        return recovered;
    }
}
