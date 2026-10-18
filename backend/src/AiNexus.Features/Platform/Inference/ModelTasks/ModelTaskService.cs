using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using System.Text;
using AiNexus.Features.Platform.Inference.ModelPolicies;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Platform.Inference.Providers;
using AiNexus.Features.Platform.Inference.Runs;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Inference.ModelTasks;

/// <summary>
/// 模型非交談任務生成結果。
/// </summary>
public sealed record ModelTaskResult(
    string Text,
    bool Truncated,
    long? InputTokens,
    long? OutputTokens);

/// <summary>
/// 非交談類模型呼叫服務（OCR、段落轉換、程式碼審查、模型評估等）。
/// 共享全域模型審批原則、Token 配額預約與計費計量。
/// 【並行架構防禦】：
/// - 透過資料庫事務更新使用者列鎖（Row Lock）串行化配額預扣；
/// - 預約完成並提交事務後，在無資料庫鎖的情況下發起外部 HTTP 串流調用，避免長連線耗盡連線池。
/// </summary>
public sealed class ModelTaskService(
    IServiceScopeFactory scopes,
    ModelCatalog catalog,
    InferenceRouter router,
    IOptions<InferenceOptions> options,
    TimeProvider clock)
{
    /// <summary>單一 Prompt 最大字元數防禦上限（16,000 字元）。</summary>
    public const int MaxPromptCharacters = 16000;

    /// <summary>預留給 Prompt framing 與系統指令的緩衝 Token 數。</summary>
    public const int FramingTokenReserve = 160;

    /// <summary>
    /// 執行單趟非交談模型生成。
    /// </summary>
    public async Task<Result<ModelTaskResult>> GenerateAsync(
        Guid owner,
        string kind,
        string prompt,
        string instruction,
        CancellationToken ct,
        string? model = null,
        IReadOnlyList<InferenceImage>? images = null,
        string? expectedConfiguration = null,
        int? maxOutputTokens = null,
        bool allowEmpty = false)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var policy = scope.ServiceProvider.GetRequiredService<ModelPolicyService>();
        var billing = scope.ServiceProvider.GetRequiredService<IModelCallMeter>();

        // 輸入過長直接拒絕，避免後面的 token 估算與配額預扣白做
        if (prompt.Length > MaxPromptCharacters || instruction.Length > 24000)
        {
            return InferenceErrors.TaskInputTooLong;
        }

        if (maxOutputTokens is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOutputTokens));
        }

        // 解析並校驗模型規格與供應商可用性
        var resolved = await catalog.RequireAsync(model, ct);
        if (!resolved.IsSuccess) return resolved.Error;
        var profile = resolved.Value;

        var outputBudget = Math.Min(maxOutputTokens ?? profile.MaxOutputTokens, profile.MaxOutputTokens);

        // 評測與代碼審查的結果必須可重現，模型組態與送審時不同就拒絕執行
        if (kind is "evaluation" or "repository-review" &&
            ModelTaskConfiguration.Require(profile, options.Value, expectedConfiguration) is { IsSuccess: false } changed)
        {
            return changed.Error;
        }

        if (images?.Count > 0 && !profile.SupportsImages)
        {
            return InferenceErrors.VisionNotSupported;
        }

        // 先確認內容能放進上下文，超出時不要產生任何配額預扣
        var estimatedPromptTokens = Encoding.UTF8.GetByteCount(prompt + instruction);
        var estimatedImageTokens = images?.Sum(x => x.EstimatedTokens) ?? 0;
        if (estimatedPromptTokens + estimatedImageTokens + outputBudget + FramingTokenReserve > profile.ContextTokens)
        {
            return InferenceErrors.ContextBudgetExceeded;
        }

        var call = new ModelInvocation
        {
            OwnerId = owner,
            Kind = kind,
            ModelId = profile.Id,
            Provider = profile.Provider,
            CreatedAt = clock.GetUtcNow()
        };

        var parameters = new GenerationParameters(
            profile.ContextTokens,
            outputBudget,
            ModelTaskConfiguration.Temperature,
            instruction,
            profile.DefaultReasoningEffort,
            profile.ReasoningControl,
            profile.SupportsImages);

        var messages = new[]
        {
            new InferenceMessage("system", instruction),
            new InferenceMessage("user", prompt, images)
        };

        var inputEstimate = MessageCost.Estimate(messages);

        // 【資料庫交易與帳號行鎖】
        // 對使用者列做一次自我更新，取得行鎖後才能讓同一帳號的配額預扣依序進行。
        // 交易在呼叫外部模型前提交，避免連線在等待網路回應期間一直被占住。
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            await db.Users
                .Where(x => x.Id == owner)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);

            var budget = await policy.BudgetAsync(owner, profile.Id, parameters, inputEstimate, call.CreatedAt, ct);
            if (!budget.IsSuccess) return budget.Error;

            parameters = budget.Value;
            call.ReservedTokens = inputEstimate + parameters.MaxOutputTokens;

            await billing.ReserveAsync(call.Id, owner, null, profile.Provider, profile.NativeId, kind, call.CreatedAt, ct);
            db.Add(call);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        var text = new StringBuilder();
        var done = false;
        string? finish = null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));

        try
        {
            await billing.StartAsync(call.Id, ct);
            await db.SaveChangesAsync(ct);

            // 依序接收供應商串流 Chunk，並即時累計用量與向計費模組回報
            await foreach (var chunk in router.StreamAsync(profile.Provider, profile.NativeId, messages, parameters, timeout.Token))
            {
                text.Append(chunk.Text);
                done |= chunk.Done;
                call.InputTokens = chunk.InputTokens ?? call.InputTokens;
                call.OutputTokens = chunk.OutputTokens ?? call.OutputTokens;

                await billing.MeterAsync(call.Id, chunk.InputTokens, chunk.OutputTokens, chunk.CachedInputTokens, chunk.ReasoningTokens, ct, chunk.Done);
                finish = chunk.FinishReason ?? finish;

                // 輸出超過 64,000 字元就中止，避免失控的模型輸出吃光記憶體
                if (text.Length > 64000)
                {
                    throw new ExternalServiceException(Error.Upstream("task_output_too_long"), "模型輸出超過處理上限。");
                }
            }

            if (!done)
            {
                throw new ExternalServiceException(Error.Upstream("task_response_incomplete"), "模型未傳回完整結果，請重試。");
            }

            call.Status = "completed";
            var output = text.ToString();

            // 若不允許空輸出但模型回傳空白，視為無有效產出（例如圖片 OCR 找不到任何文字）
            if (!allowEmpty && string.IsNullOrWhiteSpace(output))
            {
                return InferenceErrors.TaskResponseEmpty;
            }

            return new ModelTaskResult(output, finish == "MAX_TOKENS", call.InputTokens, call.OutputTokens);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            call.Status = "cancelled";
            throw;
        }
        catch (OperationCanceledException error)
        {
            call.Status = "failed";
            throw new ExternalServiceException(Error.Timeout("model_timeout"), "模型處理逾時，請稍後重試。", error);
        }
        catch
        {
            call.Status = "failed";
            throw;
        }
        finally
        {
            // 即使呼叫已被取消也要結算用量與狀態，所以這裡刻意不沿用 ct
            call.DurationMilliseconds = RunTiming.Milliseconds(call.CreatedAt, clock.GetUtcNow());
            await billing.FinishAsync(call.Id, call.Status, CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
