using AiNexus.Data.App;
using AiNexus.Data.App.Notifications;
using AiNexus.Features.AccessControl;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Notifications;

/// <summary>檢視者自己的未讀通知。通知是個人資料，因此不套用 scope 的 owner 條件。</summary>
internal sealed class NotificationsDashboard(AppDbContext db) : IDashboardSource
{
    public string Feature => FeatureIds.Dashboard;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var mine = db.Set<Notification>().AsNoTracking().Where(x => x.OwnerId == scope.Actor && x.DismissedAt == null);
        var unread = mine.Where(x => x.ReadAt == null);
        var counts = await unread.GroupBy(x => 1).Select(g => new { All = g.Count(), Errors = g.Count(x => x.Severity == "error") }).SingleOrDefaultAsync(ct);
        var daily = await DashboardDays.CountAsync(db, mine.Select(x => x.CreatedAt), scope, ct);
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
            // 失敗的顯示與通知中心一致：只用審過的問題訊息，絕不顯示儲存的原始文字。
            var error = item.Severity == "error";
            var body = error ? Issues.Message(item.IssueCode) : item.Body;
            section.Item(item.Id.ToString(), error ? "操作未完成" : item.Title, body.Length > 80 ? body[..80] + "…" : body, item.CreatedAt,
                Link(item.TargetKind, item.TargetId), error ? DashboardTones.Danger : item.Severity == "warning" ? DashboardTones.Warning : DashboardTones.Info);
        }
        return section.Build();
    }

    /// <summary>與通知中心相同的型別化跳轉目標。</summary>
    private static string? Link(string kind, Guid id) => kind switch
    {
        "conversation" => "/chat/" + id,
        "share" => "/shared/" + id,
        "task" => "/tasks?job=" + id,
        "repository-review" => "/repositories?review=" + id,
        _ => null,
    };
}
