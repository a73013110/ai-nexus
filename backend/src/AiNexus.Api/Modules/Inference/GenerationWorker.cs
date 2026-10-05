using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Inference;

public sealed class GenerationWorker(IServiceScopeFactory scopes, GenerationScheduler scheduler, IInferenceProvider provider, IOptions<InferenceOptions> options, StorageReadiness storage, ILogger<GenerationWorker> logger) : BackgroundService
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
            await foreach (var job in scheduler.Reader.ReadAllAsync(stoppingToken))
            {
                scheduler.Dequeued();
                try { await GenerateAsync(job, stoppingToken); }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError("Run {RunId} persistence failed ({ErrorType}).", job.RunId, ex.GetType().Name);
                    try
                    {
                        await scheduler.StateGate.WaitAsync(CancellationToken.None);
                        try
                        {
                            using var scope = scopes.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
                            var run = await db.Runs.SingleAsync(x => x.Id == job.RunId);
                            if (RunStates.IsActive(run.Status)) await scope.ServiceProvider.GetRequiredService<RunService>().FinishAsync(run, RunStates.Failed, "internal_error", CancellationToken.None);
                        }
                        finally { scheduler.StateGate.Release(); }
                    }
                    catch (Exception recovery) { logger.LogWarning("Run {RunId} awaits orphan recovery ({ErrorType}).", job.RunId, recovery.GetType().Name); }
                }
                finally { scheduler.Generating = false; scheduler.Finish(job); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { scheduler.Ready = false; }
    }

    private async Task GenerateAsync(GenerationJob job, CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(job.Cancellation.Token, stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        GenerationParameters parameters;
        IReadOnlyList<InferenceMessage> messages;
        string model;
        await scheduler.StateGate.WaitAsync(stoppingToken);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            var run = await db.Runs.SingleAsync(x => x.Id == job.RunId, stoppingToken);
            if (!RunStates.IsActive(run.Status) || run.ExecutorId != scheduler.InstanceId) return;
            try
            {
                var grants = await scope.ServiceProvider.GetRequiredService<AiNexus.Modules.AccessControl.AccessService>().ForUserAsync(run.OwnerId, stoppingToken);
                if (!grants.Features.Any(x => x.Id == "chat")) throw new ApiException(403, "chat_access_revoked", "對話功能權限已撤銷。");
                await scope.ServiceProvider.GetRequiredService<AiNexus.Modules.Administration.ModelPolicyService>().RequireAsync(run.OwnerId, run.ModelId, stoppingToken, checkQuota: false);
                var sources = await db.Set<AiNexus.Modules.Knowledge.MessageCitation>().Where(x => x.MessageId == run.AssistantMessageId).Select(x => new AiNexus.Modules.Knowledge.KnowledgeHitDto(x.DocumentId, x.Title, x.PageNumber, x.Excerpt, 0)).ToListAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<AiNexus.Modules.Knowledge.KnowledgeRetrieval>().ValidateHitsAsync(run.OwnerId, sources, stoppingToken);
                var projectId = await db.Conversations.Where(x => x.Id == run.ConversationId).Select(x => x.ProjectId).SingleAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<AiNexus.Modules.Projects.ProjectService>().ContextAsync(run.OwnerId, projectId, stoppingToken);
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
            messages = await scope.ServiceProvider.GetRequiredService<ContextBuilder>().BuildAsync(run.ConversationId, run.UserMessageId, parameters, stoppingToken);
            await scope.ServiceProvider.GetRequiredService<AiNexus.Modules.Billing.BillingService>().StartAsync(run.Id, stoppingToken);
            await db.SaveChangesAsync(stoppingToken);
            model = run.ModelId;
            scheduler.Generating = true;
        }
        finally { scheduler.StateGate.Release(); }
        var buffer = new StringBuilder();
        var elapsed = Stopwatch.StartNew();
        long? input = null, output = null, cached = null, reasoning = null;
        var characters = 0;
        var completed = false;
        string finalStatus = RunStates.Completed;
        string? error = null;
        try
        {
            await foreach (var chunk in provider.StreamAsync(model, messages, parameters, timeout.Token))
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
                    await FlushAsync(job.RunId, buffer.ToString(), input, output, cached, reasoning, completed, stoppingToken);
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
            logger.LogWarning("Run {RunId} provider failed: {ErrorCode} ({ErrorType}).", job.RunId, error, ex.GetType().Name);
        }
        // Request cancellation does not interrupt persistence; partial output survives.
        await FlushAsync(job.RunId, buffer.ToString(), input, output, cached, reasoning, completed, CancellationToken.None);
        await scheduler.StateGate.WaitAsync(CancellationToken.None);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            var run = await db.Runs.SingleAsync(x => x.Id == job.RunId);
            if (RunStates.IsActive(run.Status)) await scope.ServiceProvider.GetRequiredService<RunService>().FinishAsync(run, finalStatus, error, CancellationToken.None);
        }
        finally { scheduler.StateGate.Release(); }
    }

    private async Task FlushAsync(Guid id, string delta, long? input, long? output, long? cached, long? reasoning, bool complete, CancellationToken ct)
    {
        if (delta.Length == 0 && input is null && output is null) return;
        await scheduler.StateGate.WaitAsync(ct);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            var run = await db.Runs.SingleAsync(x => x.Id == id, ct);
            if (!RunStates.IsActive(run.Status)) return;
            run.Content += delta;
            run.InputTokens = input ?? run.InputTokens;
            run.OutputTokens = output ?? run.OutputTokens;
            await scope.ServiceProvider.GetRequiredService<AiNexus.Modules.Billing.BillingService>().MeterAsync(id, input, output, cached, reasoning, ct, complete);
            if (delta.Length > 0) RunService.AddEvent(db, run, "delta", delta);
            await scope.ServiceProvider.GetRequiredService<ConversationService>().UpdateAnswerAsync(run, ct);
            await db.SaveChangesAsync(ct);
        }
        finally { scheduler.StateGate.Release(); }
    }
}
