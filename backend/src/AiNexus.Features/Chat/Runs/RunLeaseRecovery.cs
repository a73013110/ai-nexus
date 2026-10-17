using AiNexus.Features.Billing;
using AiNexus.Features.Conversations;
using AiNexus.Features.Inference.Runs;
using AiNexus.Features.Notifications;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Chat.Runs;

/// <summary>
/// 生成租約逾期復原服務。
/// 當伺服器程序異常崩潰、網路中斷或重啟時，由其他健康程序接管並復原懸掛的孤兒生成。
/// 【並行防禦】：
/// - 租約超時檢查在原子性 UPDATE 條件中執行，若其他節點剛好成功續約則 UPDATE 回傳 0 行自動略過；
/// - 每個生成在所屬對話鎖（LockConversationAsync）保護下結算，避免與本機沖刷或取消競態。
/// </summary>
public sealed class RunLeaseRecovery(
    NexusDbContext db,
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

            // 原子更新：若租約已被原執行者續約，此更新影響行數為 0，安全跳過
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
