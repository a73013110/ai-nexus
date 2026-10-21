using AiNexus.Data.App;
using AiNexus.Data.App.Artifacts;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Work.Sharing;

/// <summary>
/// 分享總覽：仍有效的唯讀快照（已寄出與已收到）以及即將到期的分享。分享不等於協作；
/// 開放給協作者的資源由各自的功能統計。查詢在 <c>sharing.usp_GetSharingDashboard</c>。
/// </summary>
internal sealed class SharingDashboard(ISqlDatabase<AppDbContext> sql, TimeProvider clock) : IDashboardSource
{
    private static readonly TimeSpan Soon = TimeSpan.FromDays(7);

    public string Feature => FeatureCatalog.Sharing;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var soon = now + Soon;
        var (counts, created, kinds, inbox) = await sql.QueryDashboardAsync("sharing.usp_GetSharingDashboard", scope,
            async r => (await r.CountsAsync<Counts>(), await r.DaysAsync(), await r.SharesAsync(), await r.ItemsAsync<Share>()), ct,
            new { Now = now, ExpiringBefore = soon });
        var section = new DashboardSection("shared", Feature)
            .Count("sent", "有效分享", counts.Open, "個", hint: "未撤銷且未到期")
            .Count("expiring", "7 天內到期", counts.Expiring, "個", counts.Expiring > 0 ? DashboardTones.Warning : DashboardTones.Neutral)
            .Period("created", "期間建立", created.Sum(x => x.Value), "個")
            .Daily("created", "每日建立分享", "個", created);
        foreach (var kind in kinds) section.Share(kind.Key, Kind(kind.Key), kind.Value);
        if (scope.Owner is not null) section.Count("received", "收到的分享", counts.Received, "個");
        foreach (var share in inbox)
            section.Item(share.Id.ToString(), share.Title, Kind(share.Kind), share.CreatedAt, FeatureCatalog.RouteOf(FeatureCatalog.Sharing) + "/" + share.Id,
                share.ExpiresAt <= soon ? DashboardTones.Warning : DashboardTones.Neutral);
        return section.Build();
    }

    private static string Kind(string kind) => kind switch { ShareAccess.ConversationKind => "對話", Artifact.Kind => "成果文件", _ => kind };

    private sealed record Counts(int Open, int Expiring, int Received);

    private sealed record Share(Guid Id, string Title, string Kind, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
}
