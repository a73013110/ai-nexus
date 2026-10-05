using System.Text;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Administration;
using AiNexus.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Inference;

public sealed class ModelInvocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Kind { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string Status { get; set; } = "running";
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class ModelQuotaLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
public sealed record ModelTaskResult(string Text, bool Truncated, long? InputTokens, long? OutputTokens);

// OCR, paragraph transformations and evaluations share approval, quota and usage accounting.
// Database locks are released before the provider request starts.
public sealed class ModelTaskService(IServiceScopeFactory scopes, ModelCatalog catalog, IInferenceProvider provider, ModelQuotaLock writes, IOptions<InferenceOptions> options)
{
    public async Task<ModelTaskResult> GenerateAsync(Guid owner, string kind, string prompt, string instruction, CancellationToken ct, string? model = null, IReadOnlyList<InferenceImage>? images = null, string? expectedConfiguration = null)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var policy = scope.ServiceProvider.GetRequiredService<ModelPolicyService>();
        var billing = scope.ServiceProvider.GetRequiredService<AiNexus.Modules.Billing.BillingService>();
        if (prompt.Length > 16000 || instruction.Length > 24000) throw new ApiException(400, "task_input_too_long", "處理內容過長，請縮小選取範圍。");
        var profile = await catalog.RequireAsync(model, ct);
        if (kind == "evaluation") ModelTaskConfiguration.Require(profile, options.Value, expectedConfiguration);
        if (images?.Count > 0 && !profile.SupportsImages) throw new ApiException(400, "vision_not_supported", "系統模型不支援圖片辨識。");
        // Reject invalid input before reserving quota or recording a model invocation.
        if (Encoding.UTF8.GetByteCount(prompt + instruction) + (images?.Sum(x => x.EstimatedTokens) ?? 0) + profile.MaxOutputTokens + 160 > profile.ContextTokens)
            throw new ApiException(400, "context_budget_exceeded", "此段內容超過模型上下文，請縮小範圍或調整系統模型。");
        var call = new ModelInvocation { OwnerId = owner, Kind = kind, ModelId = profile.Id };
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // A harmless update serializes reservations for this account across application hosts.
            await db.Users.Where(x => x.Id == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
            await policy.RequireAsync(owner, profile.Id, ct);
            await billing.ReserveAsync(call.Id, owner, null, options.Value.Provider, profile.Id, kind, call.CreatedAt, ct);
            db.Add(call); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
        var text = new StringBuilder(); var done = false; string? finish = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            await billing.StartAsync(call.Id, ct); await db.SaveChangesAsync(ct);
            var parameters = new GenerationParameters(profile.ContextTokens, profile.MaxOutputTokens, ModelTaskConfiguration.Temperature, instruction, profile.DefaultReasoningEffort, profile.ReasoningControl, profile.SupportsImages);
            await foreach (var chunk in provider.StreamAsync(profile.Id, [new("user", prompt, images)], parameters, timeout.Token))
            {
                text.Append(chunk.Text); done |= chunk.Done;
                call.InputTokens = chunk.InputTokens ?? call.InputTokens; call.OutputTokens = chunk.OutputTokens ?? call.OutputTokens;
                await billing.MeterAsync(call.Id, chunk.InputTokens, chunk.OutputTokens, chunk.CachedInputTokens, chunk.ReasoningTokens, ct, chunk.Done);
                finish = chunk.FinishReason ?? finish;
                if (text.Length > 64000) throw new ApiException(502, "task_output_too_long", "模型輸出超過處理上限。");
            }
            if (!done || text.Length == 0) throw new ApiException(502, "task_response_incomplete", "模型未傳回完整結果，請重試。");
            call.Status = "completed";
            return new(text.ToString(), finish == "MAX_TOKENS", call.InputTokens, call.OutputTokens);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { call.Status = "cancelled"; throw; }
        catch (OperationCanceledException) { call.Status = "failed"; throw new ApiException(504, "model_timeout", "模型處理逾時，請稍後重試。"); }
        catch { call.Status = "failed"; throw; }
        finally { await billing.FinishAsync(call.Id, call.Status, CancellationToken.None); await db.SaveChangesAsync(CancellationToken.None); }
    }
}
public static class ModelInvocationConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var item = model.Entity<ModelInvocation>(); item.ToTable("ModelInvocations", "inference"); item.HasKey(x => x.Id);
        item.Property(x => x.Kind).HasMaxLength(32); item.Property(x => x.ModelId).HasMaxLength(160); item.Property(x => x.Status).HasMaxLength(16);
        item.HasIndex(x => new { x.OwnerId, x.CreatedAt }); item.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
