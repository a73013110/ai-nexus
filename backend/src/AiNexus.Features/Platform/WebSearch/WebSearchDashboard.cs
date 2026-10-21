using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Platform.WebSearch;

/// <summary>
/// 聊天中網路搜尋的總覽區塊；是否已設定由管理頁面顯示，不在這裡重複。
/// 查詢在 <c>websearch.usp_GetWebSearchDashboard</c>。
/// </summary>
internal sealed class WebSearchDashboard(ISqlDatabase<AppDbContext> sql) : IDashboardSource
{
    public string Feature => FeatureCatalog.Chat;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var (counts, daily) = await sql.QueryDashboardAsync("websearch.usp_GetWebSearchDashboard", scope,
            async r => (await r.CountsAsync<Counts>(), await r.DaysAsync()), ct);
        var all = daily.Sum(x => x.Value);
        return new DashboardSection("web-search", Feature, "網路搜尋", FeatureCatalog.RouteOf(FeatureCatalog.Chat))
            .Period("searches", "期間搜尋", all, "次")
            .Period("failed", "搜尋失敗", counts.Failed, "次", tone: counts.Failed > 0 ? DashboardTones.Danger : DashboardTones.Neutral)
            .Daily("searches", "每日網路搜尋", "次", daily)
            .Share("completed", "完成", all - counts.Failed, DashboardTones.Success)
            .Share("failed", "失敗", counts.Failed, DashboardTones.Danger)
            .Build();
    }

    private sealed record Counts(int Failed);
}
