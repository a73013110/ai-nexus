using System.Runtime.CompilerServices;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Inference.Providers;

/// <summary>
/// 推論路由分發器。
/// 負責根據設定檔控管各供應商的最大並行請求數（SemaphoreSlim），
/// 並將推論需求分派至對應的推論實作轉接器。
/// </summary>
public sealed class InferenceRouter(IServiceProvider services, IOptions<InferenceOptions> options)
{
    private readonly IReadOnlyDictionary<string, SemaphoreSlim> capacity = options.Value.ProviderConcurrency
        .ToDictionary(x => x.Key, x => new SemaphoreSlim(x.Value, x.Value), StringComparer.Ordinal);

    /// <summary>
    /// 取得指定供應商的推論轉接器。若未在並行組態中啟用則拒絕存取。
    /// </summary>
    public IInferenceProvider For(string provider)
    {
        if (!options.Value.ProviderConcurrency.ContainsKey(provider))
        {
            throw new ExternalServiceException(Error.Unavailable("provider_disabled"), "此模型供應商已停用，請選擇其他模型。");
        }

        return services.GetRequiredKeyedService<IInferenceProvider>(provider);
    }

    /// <summary>
    /// 取得供應商串流通道，並受該供應商的並行門閥保護。
    /// </summary>
    public async IAsyncEnumerable<InferenceChunk> StreamAsync(
        string provider,
        string model,
        IReadOnlyList<InferenceMessage> messages,
        GenerationParameters parameters,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var adapter = For(provider);

        using var activity = AiNexus.Platform.Diagnostics.DiagnosticTrace.Start(
            "model.stream",
            System.Diagnostics.Activity.Current?.TraceId.ToHexString(),
            System.Diagnostics.Activity.Current?.SpanId.ToHexString());
        activity.SetTag("external.service", provider);

        var gate = capacity[provider];
        await gate.WaitAsync(ct);

        try
        {
            await foreach (var chunk in adapter.StreamAsync(model, messages, parameters, ct))
            {
                yield return chunk;
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
