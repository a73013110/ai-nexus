using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Work.Knowledge;

/// <summary>
/// 知識庫總覽：來源文件依處理狀態統計，並列出索引分段。只計入加入知識庫的文件；私人讀取與專案文件不算知識庫文件。
/// 查詢在 <c>knowledge.usp_GetKnowledgeDashboard</c>。
/// </summary>
internal sealed class KnowledgeDashboard(ISqlDatabase<AppDbContext> sql) : IDashboardSource
{
    public string Feature => FeatureCatalog.Knowledge;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var (counts, added, recent) = await sql.QueryDashboardAsync("knowledge.usp_GetKnowledgeDashboard", scope,
            async r => (await r.CountsAsync<Counts>(), await r.DaysAsync(), await r.ItemsAsync<Recent>()), ct);
        var processing = counts.Documents - counts.Ready - counts.Failed;
        var section = new DashboardSection("knowledge", Feature)
            .Count("collections", "知識庫", counts.Collections, "個")
            .Count("documents", "知識庫文件", counts.Documents, "份", hint: $"{counts.Ready} 份可查詢 · {counts.Pages:N0} 頁")
            .Count("chunks", "索引分段", counts.Chunks, "段")
            .Count("processing", "處理中文件", processing, "份", processing > 0 ? DashboardTones.Info : DashboardTones.Neutral)
            .Count("failed", "處理失敗", counts.Failed, "份", counts.Failed > 0 ? DashboardTones.Danger : DashboardTones.Neutral)
            .Period("added", "期間新增文件", added.Sum(x => x.Value), "份")
            .Daily("added", "每日新增知識庫文件", "份", added)
            .Share("ready", "可查詢", counts.Ready, DashboardTones.Success)
            .Share("processing", "處理中", processing, DashboardTones.Info)
            .Share("failed", "處理失敗", counts.Failed, DashboardTones.Danger)
            .ActiveWhen(processing);
        foreach (var item in recent) section.Item(item.Id.ToString(), item.Name, null, item.UpdatedAt, FeatureCatalog.RouteOf(FeatureCatalog.Knowledge) + "/" + item.Id);
        return section.Build();
    }

    private sealed record Counts(int Collections, int Documents, int Ready, int Failed, int Pages, int Chunks);

    private sealed record Recent(Guid Id, string Name, DateTimeOffset UpdatedAt);
}
