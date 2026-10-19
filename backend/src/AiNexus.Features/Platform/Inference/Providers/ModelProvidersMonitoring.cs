using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Platform.Inference.Providers;

/// <summary>
/// 模型供應商運行狀態監控來源。
/// 依賴 <see cref="ModelCatalog"/> 快取的探測結果，本身不獨立進行 HTTP 探測；
/// 當快取過期時，由目錄服務既有的 single-flight 機制刷新狀態。
/// </summary>
internal sealed class ModelProvidersMonitoring(ModelCatalog catalog) : IMonitoringSource
{
    public string Id => "model-providers";
    public string Name => "模型供應商";
    public int Order => 20;
    public TimeSpan Refresh => TimeSpan.FromSeconds(15);

    public async Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var providers = await ReadDetachedAsync(ct);
        var down = providers.Where(x => !x.Available).Select(x => Display(x.Id)).ToArray();

        var (status, summary) = providers.Count switch
        {
            0 => (SubsystemStates.Unobserved, (string?)null),
            _ when down.Length == providers.Count => (SubsystemStates.Failing, "所有模型供應商都無法連線，無法生成回答。"),
            _ when down.Length > 0 => (SubsystemStates.Degraded, $"{string.Join("、", down)} 無法連線，其餘模型可正常使用。"),
            _ => (SubsystemStates.Healthy, $"{providers.Count} 個供應商皆可使用。")
        };

        var facts = providers.Select(x => SubsystemFact.Words(
            x.Id,
            Display(x.Id),
            x.Available ? "可用" : "無法連線",
            x.Available ? SubsystemTones.Positive : SubsystemTones.Danger
        )).ToArray();

        return new(status, summary, facts);
    }

    // 監控讀取會在抑制觀察者流量的狀態下執行。
    // 但目錄服務可能觸發的模型探測屬於正常應用程式流量，因此在乾淨的執行環境中執行以保留依賴指標。
    // SuppressFlow 必須在同一執行緒還原，所以只包住 Task.Run 的排入，不跨 await。
    private Task<IReadOnlyList<ProviderStatusDto>> ReadDetachedAsync(CancellationToken ct)
    {
        if (ExecutionContext.IsFlowSuppressed()) return Task.Run(() => catalog.ProviderStatusesAsync(ct), ct);
        using (ExecutionContext.SuppressFlow()) return Task.Run(() => catalog.ProviderStatusesAsync(ct), ct);
    }

    private static string Display(string provider) => provider switch
    {
        "google" => "Google AI",
        "ollama" => "Ollama",
        _ => provider
    };
}
