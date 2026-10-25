using AiNexus.Platform.Monitoring;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.WebSearch;

/// <summary>
/// 連網搜尋的執行期監控，只依設定判斷、不實際呼叫搜尋服務（實際呼叫以 dependency 指標呈現）。
/// 與聊天頁的提示共用 <see cref="WebSearchService.StatusOf"/>，兩邊對「可用」的判定不會分歧。
/// </summary>
internal sealed class WebSearchMonitoring(IOptions<WebSearchOptions> options) : IMonitoringSource
{
    public string Id => "web-search";
    public string Name => "連網搜尋";
    public int Order => 80;
    public TimeSpan Refresh => TimeSpan.FromSeconds(60);

    public Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var web = WebSearchService.StatusOf(settings);
        var provider = settings.UsesBrave ? "Brave Search" : "SearXNG";
        var (status, summary, setup, tone) =
            !settings.Enabled ? (SubsystemStates.Idle, "未啟用連網搜尋。", "未啟用", SubsystemTones.Neutral)
            : !web.Available ? (SubsystemStates.Degraded, web.Notice, "設定不完整", SubsystemTones.Warning)
            : (SubsystemStates.Healthy, $"使用 {provider}。", "已設定", SubsystemTones.Positive);
        IReadOnlyList<SubsystemFact> facts = [
            SubsystemFact.Words("setup", "設定", setup, tone),
            SubsystemFact.Words("provider", "供應商", provider),
            SubsystemFact.Count("daily_limit", "每人每日上限", settings.MaxDailyRequests),
        ];
        return Task.FromResult(new SubsystemReading(status, summary, facts));
    }
}
