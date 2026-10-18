using AiNexus.Data.App;
using AiNexus.Features.Monitoring.Traffic;

namespace AiNexus.Features.Monitoring.Snapshots;

/// <summary>管理員讀取即時監控的入口：只接受 <see cref="RuntimeTraffic"/> 保留的時間窗，且釋出前會先稽核。</summary>
internal static class MonitoringReads
{
    public static bool ValidWindow(int minutes) => minutes is 1 or 5 or 15;

    public static async Task AuditAsync(AppDbContext db, Guid user, string action, CancellationToken ct)
    {
        db.AuditEvents.Add(new() { OwnerId = user, Action = action, ResourceId = user, DetailsJson = "{\"scope\":\"current_instance\"}" });
        await db.SaveChangesAsync(ct);
    }
}
