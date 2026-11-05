using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Platform.Modules;
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
/// 交給 <see cref="ModelTaskService.GenerateAsync"/> 的工具。<see cref="InvokeAsync"/> 以模型的呼叫與可用的位元組數執行一次工具，
/// 回傳交給模型的結果文字（要自己裁到放得下）；<see cref="MaxRounds"/> 是最多幾輪可以呼叫工具。
/// </summary>
public sealed record ModelTools(
    IReadOnlyList<InferenceTool> Definitions,
    Func<InferenceToolCall, int, CancellationToken, Task<string>> InvokeAsync,
    int MaxRounds = 3);

/// <summary>
/// 非交談類模型呼叫服務（OCR、段落轉換、程式碼審查、模型評估、助理等）。
/// 共享全域模型審批原則、Token 配額預約與計費計量。
/// 【並行架構防禦】：
/// - 透過資料庫事務更新使用者列鎖（Row Lock）串行化配額預扣；
/// - 預約完成並提交事務後，在無資料庫鎖的情況下發起外部 HTTP 串流調用，避免長連線耗盡連線池。
/// </summary>
[Scoped]
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
    /// 執行非交談模型生成。<paramref name="history"/> 是放在提問之前的先前輪次（user／assistant 交替），
    /// 長度與提問合計受 <see cref="MaxPromptCharacters"/> 限制；<paramref name="onText"/> 在每段文字到達時呼叫，
    /// 讓呼叫端邊生成邊轉送（例如助理的 SSE）。驗證、配額與上下文的錯誤都在第一段文字之前回傳。
    /// <paramref name="tools"/> 有值且模型支援工具時，模型可以要求呼叫工具：結果接在對話後再問一輪，最多
    /// <see cref="ModelTools.MaxRounds"/> 輪，之後不再給工具，一定以文字作答。每一輪各自預扣配額、各記一筆用量。
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
        bool allowEmpty = false,
        IReadOnlyList<InferenceMessage>? history = null,
        Func<string, CancellationToken, ValueTask>? onText = null,
        ModelTools? tools = null)
    {
        // 輸入過長直接拒絕，避免後面的 token 估算與配額預扣白做
        history ??= [];
        if (prompt.Length + history.Sum(x => x.Content.Length) > MaxPromptCharacters || instruction.Length > 24000)
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

        List<InferenceMessage> messages = [new("system", instruction), .. history, new("user", prompt, images)];
        var offered = profile.SupportsTools && tools is { Definitions.Count: > 0 } ? tools : null;
        ModelTaskResult? spent = null;

        for (var round = 0; ; round++)
        {
            var definitions = offered is not null && round < offered.MaxRounds ? offered.Definitions : null;
            var parameters = new GenerationParameters(
                profile.ContextTokens,
                outputBudget,
                ModelTaskConfiguration.Temperature,
                instruction,
                profile.DefaultReasoningEffort,
                profile.ReasoningControl,
                profile.SupportsImages,
                definitions);

            // 先確認內容能放進上下文，超出時不要產生任何配額預扣
            if (Remaining(profile, outputBudget, messages, definitions) < 0)
            {
                return InferenceErrors.ContextBudgetExceeded;
            }

            var called = await CallAsync(owner, kind, profile, messages, parameters, onText, ct);
            if (!called.IsSuccess) return called.Error;
            var (result, calls) = called.Value;
            spent = new(result.Text, result.Truncated, Sum(spent?.InputTokens, result.InputTokens), Sum(spent?.OutputTokens, result.OutputTokens));

            if (definitions is null || calls.Count == 0)
            {
                // 若不允許空輸出但模型回傳空白，視為無有效產出（例如圖片 OCR 找不到任何文字）
                return !allowEmpty && string.IsNullOrWhiteSpace(spent.Text) ? InferenceErrors.TaskResponseEmpty : spent;
            }

            // 工具結果平分剩下的上下文；工具說明也扣掉，下一輪仍給工具時才放得下
            messages.Add(new("assistant", result.Text, ToolCalls: calls));
            var share = Remaining(profile, outputBudget, messages, definitions) / calls.Count;
            foreach (var call in calls)
            {
                messages.Add(new("tool", await offered!.InvokeAsync(call, Math.Max(0, share), ct), ToolName: call.Name));
            }
        }
    }

    /// <summary>
    /// 上下文扣掉輸出、<see cref="FramingTokenReserve"/>、訊息與工具說明後還剩多少（以 UTF-8 位元組估算）。
    /// 呼叫端依同一個算法排內容（例如助理的提示詞），排進來的就一定不會被拒絕。
    /// </summary>
    private static int Remaining(ModelDefinition profile, int output, IReadOnlyList<InferenceMessage> messages, IReadOnlyList<InferenceTool>? tools)
    {
        static long Bytes(string text) => Encoding.UTF8.GetByteCount(text);
        var used = messages.Sum(x => Bytes(x.Content) + (x.Images ?? []).Sum(i => (long)i.EstimatedTokens)
                + (x.ToolCalls ?? []).Sum(c => Bytes(c.Name + c.Arguments.ToJsonString())))
            + (tools ?? []).Sum(x => Bytes(x.Name + x.Description + x.Parameters.ToJsonString()));
        return (int)Math.Clamp(profile.ContextTokens - output - FramingTokenReserve - used, int.MinValue, int.MaxValue);
    }

    private static long? Sum(long? first, long? second) => first is null && second is null ? null : (first ?? 0) + (second ?? 0);

    /// <summary>一輪模型呼叫：預扣配額、串流、結算用量；回傳文字與模型要求的工具呼叫。</summary>
    private async Task<Result<(ModelTaskResult Result, IReadOnlyList<InferenceToolCall> Calls)>> CallAsync(
        Guid owner,
        string kind,
        ModelDefinition profile,
        IReadOnlyList<InferenceMessage> messages,
        GenerationParameters parameters,
        Func<string, CancellationToken, ValueTask>? onText,
        CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var policy = scope.ServiceProvider.GetRequiredService<ModelPolicyService>();
        var billing = scope.ServiceProvider.GetRequiredService<IModelCallMeter>();

        var call = new ModelInvocation
        {
            OwnerId = owner,
            Kind = kind,
            ModelId = profile.Id,
            Provider = profile.Provider,
            CreatedAt = clock.GetUtcNow()
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
        var calls = new List<InferenceToolCall>();
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
                if (onText is not null && chunk.Text.Length > 0) await onText(chunk.Text, ct);
                if (chunk.ToolCalls is { } requested) calls.AddRange(requested);
                done |= chunk.Done;
                call.InputTokens = chunk.InputTokens ?? call.InputTokens;
                call.OutputTokens = chunk.OutputTokens ?? call.OutputTokens;

                await billing.MeterAsync(call.Id, chunk.InputTokens, chunk.OutputTokens, chunk.CachedInputTokens, chunk.ReasoningTokens, ct, chunk.Done);
                finish = chunk.FinishReason ?? finish;

                // 輸出超過 64,000 字元就中止，避免失控的模型輸出吃光記憶體
                if (text.Length > 64000)
                {
                    throw new ExternalServiceException(InferenceErrors.TaskOutputTooLong, "模型輸出超過處理上限。");
                }
            }

            if (!done)
            {
                throw new ExternalServiceException(InferenceErrors.TaskResponseIncomplete, "模型未傳回完整結果，請重試。");
            }

            call.Status = "completed";
            return (new ModelTaskResult(text.ToString(), finish == "MAX_TOKENS", call.InputTokens, call.OutputTokens), calls);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            call.Status = "cancelled";
            throw;
        }
        catch (OperationCanceledException error)
        {
            call.Status = "failed";
            throw new ExternalServiceException(InferenceErrors.ModelTimeout, "模型處理逾時，請稍後重試。", error);
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
