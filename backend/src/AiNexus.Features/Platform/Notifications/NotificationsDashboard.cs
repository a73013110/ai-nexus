using AiNexus.Data.App;
using AiNexus.Data.App.Notifications;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Platform.Notifications;

/// <summary>檢視者自己的未讀通知。通知是個人資料，因此不套用 scope 的 owner 條件。</summary>
internal sealed class NotificationsDashboard(AppDbContext db) : IDashboardSource
{
    public string Feature => FeatureCatalog.Dashboard;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var mine = db.Set<Notification>().AsNoTracking().Inbox(scope.Actor);
        var unread = mine.Where(x => x.ReadAt == null);
        var counts = await unread.GroupBy(x => 1).Select(g => new { All = g.Count(), Errors = g.Count(x => x.Severity == NotificationDisplay.Error) }).SingleOrDefaultAsync(ct);
        var daily = await DashboardDays.CountAsync(mine.Select(x => x.CreatedAt), scope, ct);
        var errors = counts?.Errors ?? 0;
        var section = new DashboardSection("notifications", Feature, "通知", "")
            .Count("unread", "未讀通知", counts?.All ?? 0, "則", (counts?.All ?? 0) > 0 ? DashboardTones.Info : DashboardTones.Neutral)
            .Count("errors", "未讀錯誤", errors, "則", errors > 0 ? DashboardTones.Danger : DashboardTones.Neutral)
            .Period("received", "期間收到", daily.Sum(x => x.Value), "則")
            .Daily("received", "每日通知", "則", daily);
        var recent = await unread.OrderByDescending(x => x.CreatedAt).Take(6)
            .Select(x => new { x.Id, x.Title, x.Body, x.Severity, x.IssueCode, x.TargetKind, x.TargetId, x.CreatedAt }).ToListAsync(ct);
        foreach (var item in recent)
        {
            var body = NotificationDisplay.Body(item.Severity, item.Body, item.IssueCode);
            section.Item(item.Id.ToString(), NotificationDisplay.Title(item.Severity, item.Title), body.Length > 80 ? body[..80] + "…" : body, item.CreatedAt,
                NotificationDisplay.Link(item.TargetKind, item.TargetId), item.Severity == NotificationDisplay.Error ? DashboardTones.Danger : DashboardTones.Info);
        }
        return section.Build();
    }
}
