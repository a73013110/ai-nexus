using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Work.Projects;

/// <summary>
/// 專案總覽：自己擁有的、已封存的、提示詞範本數，以及單一擁有者時別人分享給他的專案。
/// 查詢在 <c>projects.usp_GetProjectsDashboard</c>。
/// </summary>
internal sealed class ProjectsDashboard(ISqlDatabase<AppDbContext> sql) : IDashboardSource
{
    public string Feature => FeatureCatalog.Projects;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var (counts, created, recent) = await sql.QueryDashboardAsync("projects.usp_GetProjectsDashboard", scope,
            async r => (await r.CountsAsync<Counts>(), await r.DaysAsync(), await r.ItemsAsync<Recent>()), ct);
        var active = counts.Projects - counts.Archived;
        var section = new DashboardSection("projects", Feature)
            .Count("projects", "專案", active, "個", hint: counts.Archived > 0 ? $"另有 {counts.Archived} 個封存" : null)
            .Count("templates", "提問範本", counts.Templates, "個")
            .Period("created", "期間建立", created.Sum(x => x.Value), "個")
            .Daily("created", "每日建立專案", "個", created);
        if (scope.Owner is not null) section.Count("shared", "共用給我", counts.Shared, "個");
        section.Share("active", "進行中", active, DashboardTones.Info).Share("archived", "已封存", counts.Archived);
        foreach (var item in recent) section.Item(item.Id.ToString(), item.Name, null, item.UpdatedAt, FeatureCatalog.RouteOf(FeatureCatalog.Projects) + "/" + item.Id);
        return section.Build();
    }

    private sealed record Counts(int Projects, int Archived, int Templates, int Shared);

    private sealed record Recent(Guid Id, string Name, DateTimeOffset UpdatedAt);
}
