using AiNexus.Features.Inference.Models;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Inference.Providers;

/// <summary>
/// Runtime monitoring of model provider availability, as <see cref="ModelCatalog"/> already caches it. No probing of its
/// own: a stale catalog starts its usual single-flight discovery.
/// </summary>
internal sealed class ModelProvidersMonitoring(ModelCatalog catalog) : IMonitoringSource
{
    public string Id => "model-providers";
    public string Name => "模型供應商";
    public int Order => 20;
    public TimeSpan Refresh => TimeSpan.FromSeconds(15);

    public async Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        // Monitoring reads run with observer traffic suppressed. Discovery the catalog may start here is ordinary
        // application traffic, so it runs on a clean execution context and stays visible in the dependency metrics.
        Task<IReadOnlyList<ProviderStatusDto>> read;
        if (ExecutionContext.IsFlowSuppressed()) read = Task.Run(() => catalog.ProviderStatusesAsync(ct), ct);
        else using (ExecutionContext.SuppressFlow()) read = Task.Run(() => catalog.ProviderStatusesAsync(ct), ct);
        var providers = await read;
        var down = providers.Where(x => !x.Available).Select(x => Display(x.Id)).ToArray();
        var (status, summary) =
            providers.Count == 0 ? (SubsystemStates.Unobserved, (string?)null)
            : down.Length == providers.Count ? (SubsystemStates.Failing, "所有模型供應商都無法連線，無法生成回答。")
            : down.Length > 0 ? (SubsystemStates.Degraded, string.Join("、", down) + " 無法連線，其餘模型可正常使用。")
            : (SubsystemStates.Healthy, $"{providers.Count} 個供應商皆可使用。");
        return new(status, summary, providers.Select(x => SubsystemFact.Words(x.Id, Display(x.Id), x.Available ? "可用" : "無法連線",
            x.Available ? SubsystemTones.Positive : SubsystemTones.Danger)).ToArray());
    }

    private static string Display(string provider) => provider switch { "google" => "Google AI", "ollama" => "Ollama", _ => provider };
}
