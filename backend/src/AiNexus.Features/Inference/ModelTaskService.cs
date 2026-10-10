using System.Text;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference;

public sealed record ModelTaskResult(string Text, bool Truncated, long? InputTokens, long? OutputTokens);

// OCR, paragraph transformations and evaluations share approval, quota and usage accounting.
// Database locks are released before the provider request starts.
public sealed class ModelTaskService(IServiceScopeFactory scopes, ModelCatalog catalog, InferenceRouter router, IOptions<InferenceOptions> options, TimeProvider clock)
{
    public const int MaxPromptCharacters = 16000;
    public const int FramingTokenReserve = 160;
    public async Task<Result<ModelTaskResult>> GenerateAsync(Guid owner, string kind, string prompt, string instruction, CancellationToken ct, string? model = null, IReadOnlyList<InferenceImage>? images = null, string? expectedConfiguration = null, int? maxOutputTokens = null)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var policy = scope.ServiceProvider.GetRequiredService<ModelPolicyService>();
        var billing = scope.ServiceProvider.GetRequiredService<IModelCallMeter>();
        if (prompt.Length > MaxPromptCharacters || instruction.Length > 24000) return InferenceErrors.TaskInputTooLong;
        if (maxOutputTokens is <= 0) throw new ArgumentOutOfRangeException(nameof(maxOutputTokens));
        var resolved = await catalog.RequireAsync(model, ct);
        if (!resolved.IsSuccess) return resolved.Error;
        var profile = resolved.Value;
        var outputBudget = Math.Min(maxOutputTokens ?? profile.MaxOutputTokens, profile.MaxOutputTokens);
        if (kind is "evaluation" or "repository-review" && ModelTaskConfiguration.Require(profile, options.Value, expectedConfiguration) is { IsSuccess: false } changed) return changed.Error;
        if (images?.Count > 0 && !profile.SupportsImages) return InferenceErrors.VisionNotSupported;
        // Reject invalid input before reserving quota or recording a model invocation.
        if (Encoding.UTF8.GetByteCount(prompt + instruction) + (images?.Sum(x => x.EstimatedTokens) ?? 0) + outputBudget + FramingTokenReserve > profile.ContextTokens)
            return InferenceErrors.ContextBudgetExceeded;
        var call = new ModelInvocation { OwnerId = owner, Kind = kind, ModelId = profile.Id, Provider = profile.Provider, CreatedAt = clock.GetUtcNow() };
        var parameters = new GenerationParameters(profile.ContextTokens, outputBudget, ModelTaskConfiguration.Temperature, instruction, profile.DefaultReasoningEffort, profile.ReasoningControl, profile.SupportsImages);
        var messages = new[] { new InferenceMessage("system", instruction), new InferenceMessage("user", prompt, images) };
        var inputEstimate = MessageCost.Estimate(messages);
        // The owner row lock taken first in this transaction serializes the reservation; it ends before the provider call.
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // A harmless update serializes reservations for this account across application hosts.
            await db.Users.Where(x => x.Id == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
            var budget = await policy.BudgetAsync(owner, profile.Id, parameters, inputEstimate, call.CreatedAt, ct);
            if (!budget.IsSuccess) return budget.Error;
            parameters = budget.Value;
            call.ReservedTokens = inputEstimate + parameters.MaxOutputTokens;
            await billing.ReserveAsync(call.Id, owner, null, profile.Provider, profile.NativeId, kind, call.CreatedAt, ct);
            db.Add(call); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        var text = new StringBuilder(); var done = false; string? finish = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            await billing.StartAsync(call.Id, ct); await db.SaveChangesAsync(ct);
            // Providers such as Ollama only receive the message list, so the task instruction must travel as a system message.
            await foreach (var chunk in router.StreamAsync(profile.Provider, profile.NativeId, messages, parameters, timeout.Token))
            {
                text.Append(chunk.Text); done |= chunk.Done;
                call.InputTokens = chunk.InputTokens ?? call.InputTokens; call.OutputTokens = chunk.OutputTokens ?? call.OutputTokens;
                await billing.MeterAsync(call.Id, chunk.InputTokens, chunk.OutputTokens, chunk.CachedInputTokens, chunk.ReasoningTokens, ct, chunk.Done);
                finish = chunk.FinishReason ?? finish;
                if (text.Length > 64000) throw new ExternalServiceException(Error.Upstream("task_output_too_long"), "模型輸出超過處理上限。");
            }
            if (!done || text.Length == 0) throw new ExternalServiceException(Error.Upstream("task_response_incomplete"), "模型未傳回完整結果，請重試。");
            call.Status = "completed";
            return new ModelTaskResult(text.ToString(), finish == "MAX_TOKENS", call.InputTokens, call.OutputTokens);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { call.Status = "cancelled"; throw; }
        catch (OperationCanceledException error) { call.Status = "failed"; throw new ExternalServiceException(Error.Timeout("model_timeout"), "模型處理逾時，請稍後重試。", error); }
        catch { call.Status = "failed"; throw; }
        finally { call.DurationMilliseconds = RunTiming.Milliseconds(call.CreatedAt, clock.GetUtcNow()); await billing.FinishAsync(call.Id, call.Status, CancellationToken.None); await db.SaveChangesAsync(CancellationToken.None); }
    }
}
