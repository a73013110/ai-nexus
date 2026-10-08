using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace AiNexus.Features.Inference;

/// <summary>Adapters own wire protocols; the router owns server-approved provider selection.</summary>
public sealed class InferenceRouter(IServiceProvider services, IOptions<InferenceOptions> options)
{
    private readonly IReadOnlyDictionary<string, SemaphoreSlim> capacity = options.Value.ProviderConcurrency.ToDictionary(x => x.Key, x => new SemaphoreSlim(x.Value, x.Value), StringComparer.Ordinal);
    public IInferenceProvider For(string provider)
    {
        if (!options.Value.ProviderConcurrency.ContainsKey(provider))
            throw new ApiException(503, "provider_disabled", "此模型供應商已停用，請選擇其他模型。");
        return services.GetRequiredKeyedService<IInferenceProvider>(provider);
    }

    public async IAsyncEnumerable<InferenceChunk> StreamAsync(string provider, string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, [EnumeratorCancellation] CancellationToken ct)
    {
        var adapter = For(provider);
        using var activity = AiNexus.Platform.Diagnostics.DiagnosticTrace.Start("model.stream", System.Diagnostics.Activity.Current?.TraceId.ToHexString(), System.Diagnostics.Activity.Current?.SpanId.ToHexString());
        activity.SetTag("external.service", provider);
        var gate = capacity[provider];
        await gate.WaitAsync(ct);
        try { await foreach (var chunk in adapter.StreamAsync(model, messages, parameters, ct)) yield return chunk; }
        finally { gate.Release(); }
    }
}
