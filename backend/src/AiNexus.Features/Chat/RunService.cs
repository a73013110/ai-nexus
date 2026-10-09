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
public sealed class RunService(NexusDbContext db, RunSignals signals, ConversationService conversations, BillingService billing, AiNexus.Features.Notifications.NotificationService notifications, Issues issues, ILogger<RunService> logger, TimeProvider clock)
{
    /// <summary>Throwing form of <see cref="FindOwnedAsync"/>, used by the event stream.</summary>
    public async Task<GenerationRun> OwnedAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var run = await FindOwnedAsync(owner, id, ct);
        return run.IsSuccess ? run.Value : throw new ApiException(404, "run_not_found", "找不到這次生成。");
    }

    /// <summary>The owner's run. Its conversation must still be theirs; otherwise <see cref="ConversationService.OwnedAsync"/> throws.</summary>
    internal async Task<Result<GenerationRun>> FindOwnedAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var run = await db.Runs.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct);
        if (run is null) return InferenceErrors.RunNotFound;
        await conversations.OwnedAsync(owner, run.ConversationId, ct);
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
            run.IssueCode = issueCode ?? issues.Report(new ApiException(503, error ?? "generation_failed", ""), error ?? "generation_failed");
        }
        logger.LogInformation(DiagnosticEvents.RunFinished, "Generation finished with {Stage}.", status);
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
}
