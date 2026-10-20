using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>清理的筆數，測試與日後的監控用。</summary>
public sealed record MailPurge(int Expired, int Contents, int Recipients, int Records);

/// <summary>
/// 每小時依 <c>Mail:Retention</c> 刪內文、清收件原址、刪舊紀錄；只處理已結束（sent、failed、cancelled）的信。
/// 每批 <see cref="BatchSize"/> 筆自動提交，避免鎖升級與長交易。傳輸停用或斷路器長時間開著時，過了 <c>ExpiresAt</c> 的信在這裡結束。
/// </summary>
public sealed partial class MailRetentionWorker(
    IServiceScopeFactory scopes,
    StorageReadiness readiness,
    IOptions<MailOptions> options,
    ILogger<MailRetentionWorker> logger,
    TimeProvider clock) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    public const int BatchSize = 1000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (readiness.Configured) await PurgeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogRetentionFailed(logger, ex.GetType().Name);
            }
            await Task.Delay(Interval, clock, stoppingToken);
        }
    }

    public async Task<MailPurge> PurgeAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var retention = options.Value.Retention;
        var now = clock.GetUtcNow();
        var messages = db.Set<OutboundMessage>();
        var finished = messages.Where(x => x.Status == MailStates.Sent || x.Status == MailStates.Failed || x.Status == MailStates.Cancelled);

        var expired = await BatchesAsync(() => messages.Where(x => x.Status == MailStates.Queued && x.ExpiresAt <= now).Take(BatchSize)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, MailStates.Failed).SetProperty(x => x.ErrorCode, MailFailures.Expired)
                .SetProperty(x => x.ContentPurgedAt, now).SetProperty(x => x.UpdatedAt, now), ct));

        // 先標記再刪內文：敏感信正常寄出時已刪，這裡是保險（例如寫回後刪除前中止）。
        var contentBefore = now.AddDays(-retention.ContentDays);
        var contents = await BatchesAsync(() => finished.Where(x => x.ContentPurgedAt == null && (x.Sensitive || x.CreatedAt < contentBefore)).Take(BatchSize)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.ContentPurgedAt, now), ct));
        await BatchesAsync(() => db.Set<OutboundMessageContent>().Where(c => messages.Any(m => m.Id == c.MessageId && m.ContentPurgedAt != null)).Take(BatchSize).ExecuteDeleteAsync(ct));

        var recipientBefore = now.AddDays(-retention.RecipientDays);
        var recipients = await BatchesAsync(() => finished.Where(x => x.RecipientAddress != null && x.CreatedAt < recipientBefore).Take(BatchSize)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.RecipientAddress, (string?)null).SetProperty(x => x.RecipientName, (string?)null), ct));

        // 內文與嘗試由資料庫的 ON DELETE CASCADE 一併刪除。
        var recordBefore = now.AddDays(-retention.RecordDays);
        var records = await BatchesAsync(() => finished.Where(x => x.CreatedAt < recordBefore).Take(BatchSize).ExecuteDeleteAsync(ct));
        return new(expired, contents, recipients, records);
    }

    private static async Task<int> BatchesAsync(Func<Task<int>> batch)
    {
        var total = 0;
        for (var count = await batch(); count > 0; count = count == BatchSize ? await batch() : 0) total += count;
        return total;
    }

    [LoggerMessage(EventId = 3306, EventName = "mail.retention_failed", Level = LogLevel.Warning, Message = "Mail retention cleanup failed ({ErrorType}).")]
    private static partial void LogRetentionFailed(ILogger logger, string errorType);
}
