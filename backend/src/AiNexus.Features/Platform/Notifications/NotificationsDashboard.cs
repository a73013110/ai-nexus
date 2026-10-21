using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Platform.Notifications;

/// <summary>
/// 檢視者自己的未讀通知。通知是個人資料，因此不套用 scope 的 owner 條件。
/// 查詢在 <c>notifications.usp_GetNotificationsDashboard</c>，收件匣條件與 <see cref="NotificationQueries.Inbox"/> 相同。
/// </summary>
internal sealed class NotificationsDashboard(ISqlDatabase<AppDbContext> sql) : IDashboardSource
{
    public string Feature => FeatureCatalog.Dashboard;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var (counts, daily, recent) = await sql.QueryDashboardAsync("notifications.usp_GetNotificationsDashboard", scope,
            async r => (await r.CountsAsync<Counts>(), await r.DaysAsync(), await r.ItemsAsync<Unread>()), ct);
        var section = new DashboardSection("notifications", Feature, "通知", "")
            .Count("unread", "未讀通知", counts.Unread, "則", counts.Unread > 0 ? DashboardTones.Info : DashboardTones.Neutral)
            .Count("errors", "未讀錯誤", counts.Errors, "則", counts.Errors > 0 ? DashboardTones.Danger : DashboardTones.Neutral)
            .Period("received", "期間收到", daily.Sum(x => x.Value), "則")
            .Daily("received", "每日通知", "則", daily);
        foreach (var item in recent)
        {
            var body = NotificationDisplay.Body(item.Severity, item.Body, item.IssueCode);
            section.Item(item.Id.ToString(), NotificationDisplay.Title(item.Severity, item.Title), body.Length > 80 ? body[..80] + "…" : body, item.CreatedAt,
                NotificationDisplay.Link(item.TargetKind, item.TargetId), item.Severity == NotificationDisplay.Error ? DashboardTones.Danger : DashboardTones.Info);
        }
        return section.Build();
    }

    private sealed record Counts(int Unread, int Errors);

    private sealed record Unread(Guid Id, string Title, string Body, string Severity, string? IssueCode, string TargetKind, Guid TargetId, DateTimeOffset CreatedAt);
}
