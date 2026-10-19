using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Data.App.Inference;
using AiNexus.Features.Admin.Billing;
using AiNexus.Features.Work.Conversations;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.Runs;
using AiNexus.Features.Platform.Notifications;
using AiNexus.Platform.Data;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Chat.Runs;

/// <summary>
/// 生成任務管理服務。
/// 提供生成紀錄查詢、結算終止狀態與生成事件累加功能，供端點、串流與背景 Worker 共用。
/// </summary>
public sealed partial class RunService(
    AppDbContext db,
    RunSignals signals,
    ConversationService conversations,
    BillingService billing,
    NotificationService notifications,
    Issues issues,
    ILogger<RunService> logger,
    TimeProvider clock)
{
    /// <summary>
    /// 查詢指定擁有者且對話未被刪除或移轉的生成記錄。
    /// </summary>
    internal async Task<Result<GenerationRun>> OwnedAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var run = await db.Runs.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct);
        if (run is null) return InferenceErrors.RunNotFound;

        if (await conversations.RequireOwnedAsync(owner, run.ConversationId, ct) is { IsSuccess: false } denied)
        {
            return denied.Error;
        }

        return run;
    }

    /// <summary>
    /// 查詢生成記錄所屬的對話識別碼（不加載回答文字），供呼叫端在取得對話鎖前先解析目標對話。
    /// </summary>
    internal async Task<Guid?> ConversationOfAsync(Guid owner, Guid id, CancellationToken ct) =>
        await db.Runs
            .Where(x => x.Id == id && x.OwnerId == owner)
            .Select(x => (Guid?)x.ConversationId)
            .SingleOrDefaultAsync(ct);

    /// <summary>
    /// 結算生成任務並終止其活躍狀態。
    /// 必須在對話鎖（LockConversationAsync）內呼叫，否則可能與背景的增量寫入互相覆蓋狀態。
    /// </summary>
    public async Task FinishAsync(
        GenerationRun run,
        string status,
        string? error,
        CancellationToken ct,
        string? issueCode = null)
    {
        using var correlation = DiagnosticTrace.Start("generation.finish", run.TraceId, run.ParentSpanId);
        correlation.SetTag("operation.id", run.Id.ToString());

        using var logging = logger.BeginScope(new Dictionary<string, object?>
        {
            ["RunId"] = run.Id,
            ["OperationId"] = run.Id,
            ["UserId"] = run.OwnerId
        });

        // 若尚未開始生成即被終止，釋放全額預約保留 Token
        if (run.StartedAt is null)
        {
            run.ReservedTokens = 0;
        }

        run.Status = status;
        run.ErrorCode = error;

        if (status == RunStates.Failed)
        {
            run.IssueCode = issueCode ?? issues.Report(Error.Unavailable(error ?? "generation_failed"));
        }

        LogFinished(logger, status);

        // 釋放活躍擁有者（解除每人單一生成限制）與租約
        run.ActiveOwnerId = null;
        run.LeaseExpiresAt = null;

        RunTiming.Finish(run, clock.GetUtcNow());
        await billing.FinishAsync(run.Id, status, ct);

        AddEvent(db, run, "status");
        await conversations.UpdateAnswerAsync(run, ct);

        db.AuditEvents.Add(new AuditEvent
        {
            OwnerId = run.OwnerId,
            Action = "run.finished",
            ResourceId = run.Id,
            Result = error ?? status
        });

        var title = await db.Conversations
            .IgnoreQueryFilters([SoftDelete.Filter])
            .Where(x => x.Id == run.ConversationId)
            .Select(x => x.Title)
            .SingleAsync(ct);

        var notificationTone = status switch
        {
            RunStates.Failed => "error",
            RunStates.Completed => "success",
            _ => "info"
        };

        var notificationTitle = status switch
        {
            RunStates.Completed => "AI 回答已完成",
            RunStates.Failed => "AI 回答未完成",
            _ => "AI 回答已停止"
        };

        var notificationMessage = status == RunStates.Failed ? Issues.Message(run.IssueCode) : title;

        await notifications.PublishAsync(
            run.OwnerId,
            "run:" + run.Id,
            "conversation." + status,
            notificationTone,
            notificationTitle,
            notificationMessage,
            "conversation",
            run.ConversationId,
            ct,
            run.IssueCode);

        await db.SaveChangesAsync(ct);
        signals.Notify(run.Id);
    }

    /// <summary>
    /// 為生成記錄遞增序號並寫入一筆有序的重播事件。
    /// </summary>
    public static void AddEvent(AppDbContext db, GenerationRun run, string type, string? delta = null)
    {
        run.LastSequence++;
        db.RunEvents.Add(new RunEvent
        {
            RunId = run.Id,
            Sequence = run.LastSequence,
            Type = type,
            Status = run.Status,
            Delta = delta,
            ErrorCode = run.ErrorCode,
            IssueCode = run.IssueCode
        });
    }

    [LoggerMessage(EventId = 3101, EventName = "generation.finished", Level = LogLevel.Information, Message = "Generation finished with {Stage}.")]
    private static partial void LogFinished(ILogger logger, string stage);
}
