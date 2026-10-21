using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Work.Jobs;

/// <summary>
/// 持久化背景工作總覽。「處理中」指 queued 或 running；「失敗」只計 <c>failed</c>，不含其他未完成的工作。期間內各種類的數量形成分布。
/// 查詢在 <c>jobs.usp_GetJobsDashboard</c>。
/// </summary>
internal sealed class JobsDashboard(ISqlDatabase<AppDbContext> sql) : IDashboardSource
{
    private static readonly Dictionary<string, string> Kinds = new(StringComparer.Ordinal)
    {
        ["document-ingest"] = "文字擷取",
        ["document-embedding"] = "向量索引",
        ["embedding-reindex"] = "重新索引",
        ["evaluation"] = "品質評測",
        ["retrieval-eval"] = "檢索評測",
        ["repository-review"] = "程式碼 review",
        ["repository-import"] = "程式庫匯入",
    };

    public string Feature => FeatureCatalog.Jobs;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var (counts, daily, kinds, mine) = await sql.QueryDashboardAsync("jobs.usp_GetJobsDashboard", scope,
            async r => (await r.CountsAsync<Counts>(), await r.DaysAsync(), await r.SharesAsync(), await r.ItemsAsync<Job>()), ct);
        var active = counts.Queued + counts.Running;
        var section = new DashboardSection("tasks", Feature)
            .Count("active", "處理中任務", active, "件", active > 0 ? DashboardTones.Info : DashboardTones.Neutral,
                $"{counts.Running} 件執行中 · {counts.Queued} 件等待中")
            .Count("failed", "失敗任務", counts.Failed, "件", counts.Failed > 0 ? DashboardTones.Danger : DashboardTones.Neutral, "可在背景任務頁重試")
            .Period("created", "期間建立", daily.Sum(x => x.Value), "件")
            .Period("completed", "期間完成", counts.Completed, "件", tone: DashboardTones.Success)
            .Daily("created", "每日建立任務", "件", daily)
            .ActiveWhen(active);
        foreach (var kind in kinds) section.Share(kind.Key, Kinds.GetValueOrDefault(kind.Key, kind.Key), kind.Value);
        foreach (var job in mine)
            section.Item(job.Id.ToString(), job.Label.Length > 0 ? job.Label : Kinds.GetValueOrDefault(job.Kind, job.Kind),
                job.Status == RunStates.Failed ? "失敗" : job.Stage, job.UpdatedAt, FeatureCatalog.RouteOf(FeatureCatalog.Jobs) + "?job=" + job.Id,
                job.Status == RunStates.Failed ? DashboardTones.Danger : DashboardTones.Info);
        return section.Build();
    }

    private sealed record Counts(int Queued, int Running, int Failed, int Completed);

    private sealed record Job(Guid Id, string Label, string Kind, string Stage, string Status, DateTimeOffset UpdatedAt);
}
