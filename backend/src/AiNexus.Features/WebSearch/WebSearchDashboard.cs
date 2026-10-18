using AiNexus.Data.App;
using AiNexus.Data.App.WebSearch;
using AiNexus.Features.AccessControl;
using AiNexus.Platform.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.WebSearch;

/// <summary>聊天中網路搜尋的總覽區塊；是否已設定由管理頁面顯示，不在這裡重複。</summary>
internal sealed class WebSearchDashboard(AppDbContext db) : IDashboardSource
{
    public string Feature => FeatureIds.Chat;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var owner = scope.Owner;
        var searches = db.Set<WebSearchRecord>().AsNoTracking().Where(x => owner == null || x.OwnerId == owner);
        var period = searches.Where(x => x.CreatedAt >= scope.From && x.CreatedAt < scope.Until);
        var failed = await period.CountAsync(x => x.Status == "failed", ct);
        var daily = await DashboardDays.CountAsync(db, searches.Select(x => x.CreatedAt), scope, ct);
        var all = daily.Sum(x => x.Value);
        return new DashboardSection("web-search", Feature, "網路搜尋", "/chat")
            .Period("searches", "期間搜尋", all, "次")
            .Period("failed", "搜尋失敗", failed, "次", tone: failed > 0 ? DashboardTones.Danger : DashboardTones.Neutral)
            .Daily("searches", "每日網路搜尋", "次", daily)
            .Share("completed", "完成", all - failed, DashboardTones.Success)
            .Share("failed", "失敗", failed, DashboardTones.Danger)
            .Build();
    }
}
