using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Conversations;
using AiNexus.Features.Billing;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Inference;

namespace AiNexus.Features.Chat;

/// <summary>Run lookup and finishing shared by the run slices, the event stream and the generation worker.</summary>
public sealed partial class RunService(NexusDbContext db, RunSignals signals, ConversationService conversations, BillingService billing, AiNexus.Features.Notifications.NotificationService notifications, Issues issues, ILogger<RunService> logger, TimeProvider clock)
{
    /// <summary>The owner's run, while its conversation is still theirs.</summary>
    internal async Task<Result<GenerationRun>> OwnedAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var run = await db.Runs.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct);
        if (run is null) return InferenceErrors.RunNotFound;
        if (await conversations.OwnedAsync(owner, run.ConversationId, ct) is { IsSuccess: false } denied) return denied.Error;
        return run;
    }

    /// <summary>The conversation of the owner's run, read without its content, so callers can take the conversation's lock first.</summary>
    internal async Task<Guid?> ConversationOfAsync(Guid owner, Guid id, CancellationToken ct)
        => await db.Runs.Where(x => x.Id == id && x.OwnerId == owner).Select(x => (Guid?)x.ConversationId).SingleOrDefaultAsync(ct);

    // Called under the conversation's generation lock, so cancellation and token flushes cannot overwrite each other.
    public async Task FinishAsync(GenerationRun run, string status, string? error, CancellationToken ct, string? issueCode = null)
    {
        using var correlation = DiagnosticTrace.Start("generation.finish", run.TraceId, run.ParentSpanId);
        correlation.SetTag("operation.id", run.Id.ToString());
        using var logging = logger.BeginScope(new Dictionary<string, object?> { ["RunId"] = run.Id, ["OperationId"] = run.Id, ["UserId"] = run.OwnerId });
        if (run.StartedAt is null) run.ReservedTokens = 0;
        run.Status = status;
        run.ErrorCode = error;
        if (status == RunStates.Failed) {
            run.IssueCode = issueCode ?? issues.Report(Error.Unavailable(error ?? "generation_failed"));
        }
        LogFinished(logger, status);
        run.ActiveOwnerId = null;
        run.LeaseExpiresAt = null;
        RunTiming.Finish(run, clock.GetUtcNow());
        await billing.FinishAsync(run.Id, status, ct);
        AddEvent(db, run, "status");
        await conversations.UpdateAnswerAsync(run, ct);
        db.AuditEvents.Add(new AuditEvent { OwnerId = run.OwnerId, Action = "run.finished", ResourceId = run.Id, Result = error ?? status });
        var title = await db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).Where(x => x.Id == run.ConversationId).Select(x => x.Title).SingleAsync(ct);
        await notifications.PublishAsync(run.OwnerId, "run:" + run.Id, "conversation." + status,
            status == "failed" ? "error" : status == "completed" ? "success" : "info",
            status == "completed" ? "AI 回答已完成" : status == "failed" ? "AI 回答未完成" : "AI 回答已停止", status == "failed" ? Issues.Message(run.IssueCode) : title, "conversation", run.ConversationId, ct, run.IssueCode);
        await db.SaveChangesAsync(ct);
        signals.Notify(run.Id);
    }

    public static void AddEvent(NexusDbContext db, GenerationRun run, string type, string? delta = null)
    {
        run.LastSequence++;
        db.RunEvents.Add(new RunEvent { RunId = run.Id, Sequence = run.LastSequence, Type = type, Status = run.Status, Delta = delta, ErrorCode = run.ErrorCode, IssueCode = run.IssueCode });
    }

    [LoggerMessage(EventId = 3101, EventName = "generation.finished", Level = LogLevel.Information, Message = "Generation finished with {Stage}.")]
    private static partial void LogFinished(ILogger logger, string stage);
}
