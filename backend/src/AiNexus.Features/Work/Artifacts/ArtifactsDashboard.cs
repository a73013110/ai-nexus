using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Work.Artifacts;

/// <summary>成果文件總覽：目前數量，以及期間內保存的不可變版本數；查詢在 <c>artifacts.usp_GetArtifactsDashboard</c>。</summary>
internal sealed class ArtifactsDashboard(ISqlDatabase<AppDbContext> sql) : IDashboardSource
{
    public string Feature => FeatureCatalog.Artifacts;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var (counts, saved, recent) = await sql.QueryDashboardAsync("artifacts.usp_GetArtifactsDashboard", scope,
            async r => (await r.CountsAsync<Counts>(), await r.DaysAsync(), await r.ItemsAsync<Recent>()), ct);
        var section = new DashboardSection("artifacts", Feature)
            .Count("artifacts", "成果文件", counts.Artifacts, "份", hint: $"{counts.FromChat} 份由對話建立")
            .Period("versions", "期間保存版本", saved.Sum(x => x.Value), "版")
            .Daily("versions", "每日保存版本", "版", saved)
            .Share("projects", "屬於專案", counts.InProjects, DashboardTones.Info)
            .Share("personal", "個人", counts.Artifacts - counts.InProjects);
        foreach (var item in recent) section.Item(item.Id.ToString(), item.Name, null, item.UpdatedAt, FeatureCatalog.RouteOf(FeatureCatalog.Artifacts) + "/" + item.Id);
        return section.Build();
    }

    private sealed record Counts(int Artifacts, int InProjects, int FromChat);

    private sealed record Recent(Guid Id, string Name, DateTimeOffset UpdatedAt);
}
