using AiNexus.Data.App;
using AiNexus.Data.App.Sharing;
using AiNexus.Features.AccessControl;
using AiNexus.Platform.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Sharing;

/// <summary>
/// 分享總覽：仍有效的唯讀快照（已寄出與已收到）以及即將到期的分享。分享不等於協作；
/// 開放給協作者的資源由各自的功能統計。
/// </summary>
internal sealed class SharingDashboard(AppDbContext db, TimeProvider clock) : IDashboardSource
{
    private static readonly TimeSpan Soon = TimeSpan.FromDays(7);

    public string Feature => FeatureIds.Shared;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var owner = scope.Owner;
        var now = clock.GetUtcNow();
        var soon = now + Soon;
        var sent = db.Set<ShareLink>().AsNoTracking().Where(x => owner == null || x.OwnerId == owner);
        var open = sent.Where(x => !x.IsRevoked && x.ExpiresAt > now);
        var counts = await open.GroupBy(x => 1).Select(g => new { Open = g.Count(), Expiring = g.Count(x => x.ExpiresAt <= soon) }).SingleOrDefaultAsync(ct);
        var created = await DashboardDays.CountAsync(db, sent.Select(x => x.CreatedAt), scope, ct);
        var kinds = await open.GroupBy(x => x.Kind).Select(g => new { Kind = g.Key, Count = g.Count() }).ToListAsync(ct);
        var expiring = counts?.Expiring ?? 0;
        var section = new DashboardSection("shared", Feature)
            .Count("sent", "有效分享", counts?.Open ?? 0, "個", hint: "未撤銷且未到期")
            .Count("expiring", "7 天內到期", expiring, "個", expiring > 0 ? DashboardTones.Warning : DashboardTones.Neutral)
            .Period("created", "期間建立", created.Sum(x => x.Value), "個")
            .Daily("created", "每日建立分享", "個", created);
        foreach (var kind in kinds) section.Share(kind.Kind, Kind(kind.Kind), kind.Count);
        if (owner is Guid id)
        {
            var received = await db.Set<ShareRecipient>().AsNoTracking()
                .CountAsync(r => r.UserId == id && db.Set<ShareLink>().Any(s => s.Id == r.ShareId && !s.IsRevoked && s.ExpiresAt > now), ct);
            section.Count("received", "收到的分享", received, "個");
        }
        var inbox = await (from recipient in db.Set<ShareRecipient>().AsNoTracking()
                           join share in db.Set<ShareLink>().AsNoTracking() on recipient.ShareId equals share.Id
                           where recipient.UserId == scope.Actor && !share.IsRevoked && share.ExpiresAt > now
                           orderby share.CreatedAt descending
                           select new { share.Id, share.Title, share.Kind, share.CreatedAt, share.ExpiresAt }).Take(5).ToListAsync(ct);
        foreach (var share in inbox)
            section.Item(share.Id.ToString(), share.Title, Kind(share.Kind), share.CreatedAt, "/shared/" + share.Id,
                share.ExpiresAt <= soon ? DashboardTones.Warning : DashboardTones.Neutral);
        return section.Build();
    }

    private static string Kind(string kind) => kind switch { "conversation" => "對話", "artifact" => "成果文件", _ => kind };
}
