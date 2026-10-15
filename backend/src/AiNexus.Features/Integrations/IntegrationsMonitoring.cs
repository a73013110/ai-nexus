using AiNexus.Features.Integrations.Sources;
using AiNexus.Features.Repositories;
using AiNexus.Features.WebSearch;
using AiNexus.Platform.Data;
using AiNexus.Platform.Monitoring;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Integrations;

/// <summary>
/// Runtime monitoring of external integrations from configuration alone: web search, Gitea and the legacy data
/// sources. Nothing is contacted; their actual calls appear as dependencies. Degraded when an enabled integration is
/// missing part of its setup, idle when none is enabled.
/// </summary>
internal sealed class IntegrationsMonitoring(IOptions<WebSearchOptions> search, IOptions<GiteaOptions> gitea, IOptions<IntegrationsOptions> sources,
    IOptions<DatabasesOptions> databases) : IMonitoringSource
{
    public string Id => "integrations";
    public string Name => "外部整合";
    public int Order => 80;
    public TimeSpan Refresh => TimeSpan.FromSeconds(60);

    public Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var facts = new List<SubsystemFact>();
        var enabled = 0; var incomplete = new List<string>();
        var web = WebSearchService.StatusOf(search.Value);
        Add("web_search", "連網搜尋", search.Value.Enabled, web.Available);
        Add("gitea", "Gitea 程式庫", gitea.Value.Enabled, gitea.Value.Enabled);
        foreach (var (id, name, database) in SourceGateway.Sources)
        {
            var option = sources.Value.For(id);
            var setup = SourceGateway.SetupStatus(option, databases.Value.For(database).Configured);
            Add(id, name, option.Enabled, setup == "configured");
        }
        var (status, summary) =
            incomplete.Count > 0 ? (SubsystemStates.Degraded, string.Join("、", incomplete) + " 已啟用但設定不完整。")
            : enabled == 0 ? (SubsystemStates.Idle, "未啟用任何外部整合。")
            : (SubsystemStates.Healthy, $"{enabled} 項外部整合已設定。");
        return Task.FromResult(new SubsystemReading(status, summary, facts));

        void Add(string id, string label, bool on, bool ready)
        {
            if (on) enabled++;
            if (on && !ready) incomplete.Add(label);
            facts.Add(SubsystemFact.Words(id, label, !on ? "未啟用" : ready ? "已設定" : "設定不完整",
                !on ? SubsystemTones.Neutral : ready ? SubsystemTones.Positive : SubsystemTones.Warning));
        }
    }
}
