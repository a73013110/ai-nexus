using AiNexus.Data.App;
using AiNexus.Features.Admin.Mail.Delivery;
using AiNexus.Platform.Auditing;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail.Messages;

/// <summary>
/// 寄送紀錄頁首的狀態：傳輸設定（不含帳密）、斷路器、佇列與管理頁可切換的設定。<paramref name="PausedUntil"/> 有值代表傳輸層故障、暫停寄送中，
/// <paramref name="PauseReason"/> 是分類代碼。完整健康資訊在即時監控的「郵件寄送」卡。
/// </summary>
public sealed record MailStatusDto(string Transport, bool Enabled, string FromAddress, string? Server, DateTimeOffset? PausedUntil, string? PauseReason,
    int Queued, int Sending, int FailedLastHour, int SentLastDay, DateTimeOffset? OldestQueuedAt, int MaxPerMinute, int MaxPerDay, int GiveUpAfterHours,
    bool AutoRetry);

internal sealed class GetMailStatus(AppDbContext db, MailCircuit circuit, IOptions<MailOptions> options, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (GetMailStatus handler, CancellationToken ct) => handler.HandleAsync(ct))
        .NotAudited("寄送狀態摘要，不含郵件內容")
        .WithName("GetMailStatus");

    public async Task<MailStatusDto> HandleAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var queue = await MailQueue.ReadAsync(db, clock.GetUtcNow(), ct);
        var autoRetry = await MailDeliverySettings.AutoRetryAsync(db, ct);
        var server = settings.Transport switch
        {
            MailTransports.Smtp => $"{settings.Smtp.Host}:{settings.Smtp.Port}（{settings.Smtp.Security}）",
            MailTransports.Pickup => "pickup",
            _ => null,
        };
        return new(settings.Transport, settings.Enabled, settings.From.Address, server, circuit.IsOpen ? circuit.OpenUntil : null, circuit.IsOpen ? circuit.LastErrorCode : null,
            queue.Queued, queue.Sending, queue.FailedLastHour, queue.SentLastDay, queue.OldestQueuedAt, settings.MaxPerMinute, settings.MaxPerDay, settings.GiveUpAfterHours, autoRetry);
    }
}
