using System.Diagnostics;
using AiNexus.Platform.Diagnostics;
using System.Text;
using System.Text.Json;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Conversations;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Inference;

namespace AiNexus.Features.Chat;

public sealed class GenerationWorker(IServiceScopeFactory scopes, GenerationScheduler scheduler, RunSignals signals, InferenceRouter router, IOptions<InferenceOptions> options, StorageReadiness storage, ILogger<GenerationWorker> logger, Issues issues) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!storage.Configured) { logger.LogWarning("Generation disabled: storage is not configured."); return; }
        try
        {
            using var startup = scopes.CreateScope();
            var db = startup.ServiceProvider.GetRequiredService<NexusDbContext>();
            if (!await db.Database.CanConnectAsync(stoppingToken)) { logger.LogWarning("Generation disabled: storage is unavailable."); return; }
            await startup.ServiceProvider.GetRequiredService<RunLeaseRecovery>().RecoverAsync(DateTimeOffset.UtcNow, stoppingToken);
            foreach (var profile in options.Value.Models)
            {
                var existing = await db.ModelProfiles.FindAsync([profile.Id], stoppingToken);
                if (existing is null) db.ModelProfiles.Add(profile);
                else db.Entry(existing).CurrentValues.SetValues(profile);
            }
            await db.SaveChangesAsync(stoppingToken);
            scheduler.Ready = true;
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning("Generation disabled during startup ({ErrorType}). Check storage and migrations.", ex.GetType().Name);
            return;
        }
        try
        {
            await Task.WhenAll(scheduler.Workers.SelectMany(queue => Enumerable.Range(0, queue.Concurrency).Select(_ => ConsumeAsync(queue.Reader, stoppingToken))));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { scheduler.Ready = false; }
    }

    private async Task ConsumeAsync(System.Threading.Channels.ChannelReader<GenerationJob> reader, CancellationToken stoppingToken)
    {
        await foreach (var job in reader.ReadAllAsync(stoppingToken))
        {
            scheduler.Dequeued();
            scheduler.Started();
            using var activity = DiagnosticTrace.Start("generation.execute", job.TraceId, job.ParentSpanId, ActivityKind.Consumer);
            activity.SetTag("operation.id", job.RunId.ToString());
            using var logging = logger.BeginScope(new Dictionary<string, object?> { ["RunId"] = job.RunId, ["OperationId"] = job.RunId, ["UserId"] = job.OwnerId, ["Provider"] = job.Provider, ["RequestId"] = null });
            try { await GenerateAsync(job, stoppingToken); }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                var issue = issues.Report(ex, "generation_persistence_failed");
                try
                {
                    using (await scheduler.LockConversationAsync(job.ConversationId, CancellationToken.None))
                    {
                        using var scope = scopes.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
                        var run = await db.Runs.SingleAsync(x => x.Id == job.RunId);
                        if (RunStates.IsActive(run.Status)) await scope.ServiceProvider.GetRequiredService<RunService>().FinishAsync(run, RunStates.Failed, "internal_error", CancellationToken.None, issue);
                    }
                }
                catch (Exception recovery) { logger.LogWarning("Run {RunId} awaits orphan recovery ({ErrorType}).", job.RunId, recovery.GetType().Name); }
            }
            finally { scheduler.Stopped(); scheduler.Finish(job); }
        }
    }

    private async Task GenerateAsync(GenerationJob job, CancellationToken stoppingToken)
    {
        logger.LogInformation(DiagnosticEvents.RunStarted, "Generation started.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(job.Cancellation.Token, stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        GenerationParameters parameters;
        IReadOnlyList<InferenceMessage> messages;
        string model;
        string provider;
        RunProgress progress;
        using (await scheduler.LockConversationAsync(job.ConversationId, stoppingToken))
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            var run = await db.Runs.SingleAsync(x => x.Id == job.RunId, stoppingToken);
            if (!RunStates.IsActive(run.Status) || run.ExecutorId != scheduler.InstanceId) return;
            try
            {
                var grants = await scope.ServiceProvider.GetRequiredService<AiNexus.Features.AccessControl.AccessService>().ForUserAsync(run.OwnerId, stoppingToken);
                if (!grants.Features.Any(x => x.Id == "chat")) throw new ApiException(403, "chat_access_revoked", "對話功能權限已撤銷。");
                await scope.ServiceProvider.GetRequiredService<ModelPolicyService>().RequireAsync(run.OwnerId, run.ModelId, stoppingToken, checkQuota: false);
                var sources = await db.Set<AiNexus.Features.Knowledge.MessageCitation>().Where(x => x.MessageId == run.AssistantMessageId).Select(x => new AiNexus.Features.Knowledge.KnowledgeHitDto(x.DocumentId, x.Title, x.PageNumber, x.Excerpt, 0, Guid.Empty, x.EndPage)).ToListAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<AiNexus.Features.Knowledge.KnowledgeRetrieval>().ValidateHitsAsync(run.OwnerId, sources, stoppingToken);
                var projectId = await db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).Where(x => x.Id == run.ConversationId).Select(x => x.ProjectId).SingleAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<AiNexus.Features.Projects.ProjectService>().ContextAsync(run.OwnerId, projectId, stoppingToken);
            }
            catch (ApiException revoked)
            {
                await scope.ServiceProvider.GetRequiredService<RunService>().FinishAsync(run, RunStates.Failed, revoked.Code, stoppingToken);
                return;
            }
            run.Status = RunStates.Running;
            run.StartedAt = DateTimeOffset.UtcNow;
            RunService.AddEvent(db, run, "status");
            await scope.ServiceProvider.GetRequiredService<ConversationService>().UpdateAnswerAsync(run, stoppingToken);
            await db.SaveChangesAsync(stoppingToken);
            parameters = JsonSerializer.Deserialize<GenerationParameters>(run.ParametersJson)!;
            var contexts = scope.ServiceProvider.GetRequiredService<ContextBuilder>();
            // Answered history does not change, so the context CreateRun reserved for is sent as is; only image bytes are read now.
            messages = job.Context is { } prepared ? await contexts.WithImagesAsync(prepared, stoppingToken) : await contexts.BuildAsync(run.ConversationId, run.UserMessageId, parameters, stoppingToken);
            await scope.ServiceProvider.GetRequiredService<AiNexus.Features.Billing.BillingService>().StartAsync(run.Id, stoppingToken);
            await db.SaveChangesAsync(stoppingToken);
            model = run.ProviderModelId;
            provider = run.Provider;
            progress = new RunProgress(run.LastSequence, run.AssistantMessageId);
        }
        signals.Notify(job.RunId);
        var buffer = new StringBuilder();
        var elapsed = Stopwatch.StartNew();
        long? input = null, output = null, cached = null, reasoning = null;
        var characters = 0;
        var completed = false;
        string finalStatus = RunStates.Completed;
        string? error = null, issueCode = null;
        try
        {
            await foreach (var chunk in router.StreamAsync(provider, model, messages, parameters, timeout.Token))
            {
                characters += chunk.Text.Length;
                if (characters > options.Value.MaxOutputCharacters) throw new ApiException(502, "output_limit_exceeded", "回答超過文字上限，請分段提問。");
                completed |= chunk.Done;
                buffer.Append(chunk.Text);
                input = chunk.InputTokens ?? input;
                output = chunk.OutputTokens ?? output;
                cached = chunk.CachedInputTokens ?? cached; reasoning = chunk.ReasoningTokens ?? reasoning;
                if (elapsed.ElapsedMilliseconds >= 80 || buffer.Length >= 512 || chunk.Done)
                {
                    await FlushAsync(job, progress, buffer.ToString(), input, output, cached, reasoning, completed, stoppingToken);
                    buffer.Clear();
                    elapsed.Restart();
                }
            }
            if (!completed) throw new ApiException(502, "provider_stream_incomplete", "模型串流提前結束。");
        }
        catch (OperationCanceledException)
        {
            finalStatus = job.Cancellation.IsCancellationRequested ? RunStates.Cancelled : RunStates.Failed;
            error = finalStatus == RunStates.Cancelled ? null : stoppingToken.IsCancellationRequested ? "server_stopping" : "generation_timeout";
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException or ApiException)
        {
            finalStatus = RunStates.Failed;
            error = (ex as ApiException)?.Code ?? (ex is HttpRequestException or IOException ? "provider_connection_lost" : "provider_protocol_error");
            issueCode = issues.Report(ex, error);
        }
        // Request cancellation does not interrupt persistence; partial output survives.
        await FlushAsync(job, progress, buffer.ToString(), input, output, cached, reasoning, completed, CancellationToken.None);
        using (await scheduler.LockConversationAsync(job.ConversationId, CancellationToken.None))
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            var run = await db.Runs.SingleAsync(x => x.Id == job.RunId);
            if (RunStates.IsActive(run.Status)) await scope.ServiceProvider.GetRequiredService<RunService>().FinishAsync(run, finalStatus, error, CancellationToken.None, issueCode);
        }
    }

    /// <summary>What this worker has already written for its run. Flushes only append, so this is all a flush needs.</summary>
    private sealed class RunProgress(long sequence, Guid answer)
    {
        public long Sequence = sequence;
        public Guid Answer { get; } = answer;
        public long? Input, Output, Cached, Reasoning;
        public bool Complete;
    }

    /// <summary>
    /// Costs O(delta): the stored answer is appended in SQL and never read back or resent. The delta event, the run
    /// (content and sequence move together, so a snapshot at LastSequence equals the deltas up to it), the usage and the
    /// answer message are written in one transaction, and only while the run is still active, so a cancellation or a
    /// recovery is never overwritten. Usage values are cumulative: only the ones that changed are metered again.
    /// </summary>
    private async Task FlushAsync(GenerationJob job, RunProgress progress, string delta, long? input, long? output, long? cached, long? reasoning, bool complete, CancellationToken ct)
    {
        if (delta.Length == 0 && input is null && output is null) return;
        static long? Changed(long? value, long? written) => value is not null && value != written ? value : null;
        var (newInput, newOutput, newCached, newReasoning) = (Changed(input, progress.Input), Changed(output, progress.Output), Changed(cached, progress.Cached), Changed(reasoning, progress.Reasoning));
        var completes = complete && !progress.Complete;
        if (delta.Length == 0 && newInput is null && newOutput is null && newCached is null && newReasoning is null && !completes) return;
        var expected = progress.Sequence;
        using (await scheduler.LockConversationAsync(job.ConversationId, ct))
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.Database.AutoSavepointsEnabled = false;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (await AppendRunAsync(db, job.RunId, expected, delta, newInput, newOutput, ct) == 0)
            {
                var current = await db.Runs.Where(x => x.Id == job.RunId).Select(x => new { x.Status, x.LastSequence }).SingleAsync(ct);
                if (!RunStates.IsActive(current.Status)) return;
                // Another writer added an event to the still active run: continue after it.
                expected = current.LastSequence;
                if (await AppendRunAsync(db, job.RunId, expected, delta, newInput, newOutput, ct) == 0) return;
            }
            if (delta.Length > 0)
            {
                db.RunEvents.Add(new RunEvent { RunId = job.RunId, Sequence = expected + 1, Type = "delta", Status = RunStates.Running, Delta = delta });
                await db.SaveChangesAsync(ct);
            }
            await scope.ServiceProvider.GetRequiredService<AiNexus.Features.Billing.BillingService>().MeterInPlaceAsync(job.RunId, newInput, newOutput, newCached, newReasoning, completes, ct);
            await scope.ServiceProvider.GetRequiredService<ConversationService>().AppendAnswerAsync(progress.Answer, job.ConversationId, delta, ct);
            await transaction.CommitAsync(ct);
        }
        signals.Notify(job.RunId);
        progress.Sequence = expected + (delta.Length > 0 ? 1 : 0);
        progress.Input = input ?? progress.Input; progress.Output = output ?? progress.Output;
        progress.Cached = cached ?? progress.Cached; progress.Reasoning = reasoning ?? progress.Reasoning;
        progress.Complete |= complete;
    }

    private static Task<int> AppendRunAsync(NexusDbContext db, Guid id, long expected, string delta, long? input, long? output, CancellationToken ct)
        => db.Runs.Where(x => x.Id == id && x.LastSequence == expected && (x.Status == RunStates.Queued || x.Status == RunStates.Running))
            .ExecuteUpdateAsync(setters =>
            {
                setters.SetProperty(x => x.LastSequence, expected + (delta.Length > 0 ? 1 : 0));
                if (delta.Length > 0) setters.SetProperty(x => x.Content, x => x.Content + delta);
                if (input is long inputTokens) setters.SetProperty(x => x.InputTokens, inputTokens);
                if (output is long outputTokens) setters.SetProperty(x => x.OutputTokens, outputTokens);
            }, ct);
}
