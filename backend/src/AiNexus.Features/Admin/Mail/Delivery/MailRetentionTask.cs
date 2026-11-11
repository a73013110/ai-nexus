using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Platform.Data;
using AiNexus.Platform.Scheduling;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>清理的筆數，測試與日後的監控用。</summary>
public sealed record MailPurge(int Expired, int Contents, int Recipients, int Records)
{
    public static readonly MailPurge None = new(0, 0, 0, 0);
}

/// <summary>
/// 依 <c>Mail:Retention</c> 刪內文、清收件原址、刪舊紀錄；只處理已結束（sent、failed、cancelled）的信。
/// 每項都分批執行（<see cref="Batches"/>）。傳輸停用或斷路器長時間開著時，過了 <c>ExpiresAt</c> 的信在這裡結束。
/// </summary>
public sealed class MailRetentionTask(AppDbContext db, AuditTrail audit, IOptions<MailOptions> options, TimeProvider clock) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "mail.retention", "郵件保存期清理", "郵件", "結束逾期未寄的信，依保存期刪內文、清收件原址、刪舊紀錄。");

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        var purge = await PurgeAsync(ct);
        return purge == MailPurge.None ? TaskOutcome.Idle()
            : TaskOutcome.Done($"逾期 {purge.Expired:N0}、內文 {purge.Contents:N0}、收件人 {purge.Recipients:N0}、紀錄 {purge.Records:N0}");
    }

    public async Task<MailPurge> PurgeAsync(CancellationToken ct)
    {
        var retention = options.Value.Retention;
        var now = clock.GetUtcNow();
        var finished = db.OutboundMessages.Where(x => x.Status == MailStates.Sent || x.Status == MailStates.Failed || x.Status == MailStates.Cancelled);

        var expired = await db.OutboundMessages.Where(x => x.Status == MailStates.Queued && x.ExpiresAt <= now)
            .UpdateInBatchesAsync(p => p.SetProperty(x => x.Status, MailStates.Failed).SetProperty(x => x.ErrorCode, MailFailures.Expired)
                .SetProperty(x => x.ContentPurgedAt, now).SetProperty(x => x.UpdatedAt, now), ct);

        // 先標記再刪內文：敏感信正常寄出時已刪，這裡是保險（例如寫回後刪除前中止）。
        var contentBefore = now.AddDays(-retention.ContentDays);
        var contents = await finished.Where(x => x.ContentPurgedAt == null && (x.Sensitive || x.CreatedAt < contentBefore))
            .UpdateInBatchesAsync(p => p.SetProperty(x => x.ContentPurgedAt, now), ct);
        await db.OutboundMessageContents.Where(c => db.OutboundMessages.Any(m => m.Id == c.MessageId && m.ContentPurgedAt != null)).DeleteInBatchesAsync(ct);

        var recipientBefore = now.AddDays(-retention.RecipientDays);
        var recipients = await finished.Where(x => x.RecipientAddress != null && x.CreatedAt < recipientBefore)
            .UpdateInBatchesAsync(p => p.SetProperty(x => x.RecipientAddress, (string?)null).SetProperty(x => x.RecipientName, (string?)null), ct);

        // 內文與嘗試由資料庫的 ON DELETE CASCADE 一併刪除。
        var recordBefore = now.AddDays(-retention.RecordDays);
        var records = await finished.Where(x => x.CreatedAt < recordBefore).DeleteInBatchesAsync(ct);
        // 刪內文、清收件人、刪紀錄都是依政策銷毀資料，要留證；批次各自提交，所以在全部完成後另記一筆，中途失敗的部分下一輪清完時一起記。
        var purge = new MailPurge(expired, contents, recipients, records);
        if (purge != MailPurge.None)
        {
            audit.Record(MailAudit.MessageCleared, Guid.Empty,
                details: new { automatic = true, expired, contents, recipients, records });
            await db.SaveChangesAsync(ct);
        }
        return purge;
    }
}
