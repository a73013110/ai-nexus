using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Mail.Delivery;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Mail.Messages;

/// <summary>
/// 等待中（含等待重試）的信改成現在就寄：同一列、同一個 Message-ID，嘗試次數照常累加；寫行政稽核 <c>admin.mail_retry</c>。
/// 暫停寄送中時一併結束暫停（<see cref="MailCircuit.Resume"/>），管理者通常是修好設定才按；仍然失敗會再暫停。
/// 已結束（寄出、失敗、取消）的信不能重試，要用重寄建立新信。
/// </summary>
internal sealed class RetryMailMessage(AppDbContext db, MailCircuit circuit, AdministrativeWrite write, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/retry", (Guid id, RetryMailMessage handler, CancellationToken ct) =>
            handler.HandleAsync(id, ct))
        .RequireFeature(FeatureCatalog.MailMessagesResend)
        .Audited(MailAudit.MessageRetried)
        .WithName("RetryMailMessage");

    public async Task<Result<MailMessageDto>> HandleAsync(Guid id, CancellationToken ct)
    {
        // 稽核只記狀態與下次嘗試時間，不記收件者與內容。
        var begun = await write.BeginAsync(AuditTarget.Of(id, async token =>
            await db.OutboundMessages.AsNoTracking().Where(x => x.Id == id).Select(x => new { status = x.Status, attempts = x.Attempts, nextAttemptAt = x.NextAttemptAt }).SingleOrDefaultAsync(token)), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var status = await db.OutboundMessages.Where(x => x.Id == id).Select(x => x.Status).SingleOrDefaultAsync(ct);
        if (status is null) return MailErrors.MessageNotFound;
        var now = clock.GetUtcNow();
        // 條件式更新：寄送中被認領的信不受影響，也不會把剛結束的信改回去。
        var updated = await db.OutboundMessages.Where(x => x.Id == id && x.Status == MailStates.Queued)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.NextAttemptAt, now).SetProperty(x => x.UpdatedAt, now), ct);
        if (updated != 1) return MailErrors.NotRetryable;

        var committed = await transaction.CommitAsync(ct);
        if (!committed.IsSuccess) return committed.Error;
        circuit.Resume();
        var message = await db.OutboundMessages.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        return MailMessageDto.From(message);
    }
}
