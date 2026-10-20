using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Mail.Delivery;
using AiNexus.Platform.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail;

/// <summary>
/// 郵件寄送的監控：傳輸停用 idle；斷路器開著 failing；1 小時內有失敗或最久等待超過 10 分鐘 degraded。佇列查詢走 Pending 篩選索引，失敗走 CreatedAt 索引。
/// </summary>
internal sealed class MailMonitoring(AppDbContext db, MailCircuit circuit, IOptions<MailOptions> options, TimeProvider clock) : IMonitoringSource
{
    public string Id => "mail";
    public string Name => "郵件寄送";
    public int Order => 35;
    public TimeSpan Refresh => TimeSpan.FromSeconds(30);

    public async Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var queue = await MailQueue.ReadAsync(db, now, ct);
        var waited = queue.OldestQueuedAt is { } oldest ? Math.Max(0, Math.Round((now - oldest).TotalSeconds)) : (double?)null;
        var (status, summary) =
            !settings.Enabled ? (SubsystemStates.Idle, $"郵件傳輸未啟用（{settings.Transport}），信件只會入列不會寄出。")
            : circuit.IsOpen ? (SubsystemStates.Failing, "郵件伺服器無法使用，暫停寄送中。")
            : queue.FailedLastHour > 0 ? (SubsystemStates.Degraded, $"最近 1 小時有 {queue.FailedLastHour} 封寄送失敗。")
            : waited > 600 ? (SubsystemStates.Degraded, "有信件等待超過 10 分鐘。")
            : queue.Queued + queue.Sending == 0 ? (SubsystemStates.Idle, "目前沒有待寄的信件。")
            : (SubsystemStates.Healthy, $"{queue.Sending} 封寄送中，{queue.Queued} 封等待中。");
        IReadOnlyList<SubsystemFact> facts = [
            SubsystemFact.Words("transport", "傳輸", settings.Transport),
            SubsystemFact.Count("queued", "等待中", queue.Queued),
            SubsystemFact.Count("sending", "寄送中", queue.Sending, queue.Sending > 0 ? SubsystemTones.Positive : SubsystemTones.Neutral),
            SubsystemFact.Count("failed_1h", "1 小時內失敗", queue.FailedLastHour, queue.FailedLastHour > 0 ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Seconds("oldest_queued", "最久等待", waited, waited > 600 ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Count("sent_24h", "24 小時內寄出", queue.SentLastDay),
        ];
        return new(status, summary, facts);
    }
}

/// <summary>佇列彙總；監控與管理頁的狀態列共用。</summary>
internal sealed record MailQueue(int Queued, int Sending, int FailedLastHour, int SentLastDay, DateTimeOffset? OldestQueuedAt)
{
    public static async Task<MailQueue> ReadAsync(AppDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var messages = db.Set<OutboundMessage>().AsNoTracking();
        var pending = await messages.Where(x => x.Status == MailStates.Queued || x.Status == MailStates.Sending).GroupBy(x => 1).Select(g => new
        {
            Queued = g.Count(x => x.Status == MailStates.Queued),
            Sending = g.Count(x => x.Status == MailStates.Sending),
            Oldest = g.Where(x => x.Status == MailStates.Queued).Min(x => (DateTimeOffset?)x.CreatedAt),
        }).SingleOrDefaultAsync(ct);
        var hour = now.AddHours(-1);
        var day = now.AddDays(-1);
        var failed = await messages.CountAsync(x => x.Status == MailStates.Failed && x.UpdatedAt >= hour && x.CreatedAt >= day, ct);
        var sent = await messages.CountAsync(x => x.SentAt >= day, ct);
        return new(pending?.Queued ?? 0, pending?.Sending ?? 0, failed, sent, pending?.Oldest);
    }
}
