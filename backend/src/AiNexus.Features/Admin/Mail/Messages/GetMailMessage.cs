using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Mail.Messages;

/// <summary>
/// 一次寄送嘗試；<paramref name="Response"/> 是伺服器回應或失敗細節（含 TLS 憑證摘要），寫入時已遮罩 email、網址與路徑。
/// <paramref name="Stage"/> 是失敗的步驟（<c>MailAttemptStages</c>）。
/// </summary>
public sealed record MailAttemptDto(int Attempt, string Transport, DateTimeOffset StartedAt, DateTimeOffset FinishedAt, string Outcome,
    int? SmtpStatus, string? EnhancedStatus, string? Response, string? ErrorCode, string? IssueCode, string? Stage);

/// <summary>
/// 寄送紀錄的詳情。<paramref name="Text"/> 是純文字內文預覽：敏感信或內文已清除時為 null（HTML 不回傳、不渲染）。
/// <paramref name="CanResend"/> 為 false 時 <paramref name="ResendBlockedBy"/> 是原因代碼。
/// </summary>
public sealed record MailMessageDetailDto(MailMessageDto Message, string HeaderMessageId, string FromAddress, Guid? BatchId, Guid? ResendOfId,
    Guid? RequestedById, string? RequestedBy, Guid? RecipientUserId, string? Transport, string? ProviderResponse, string? IssueCode, string? TraceId,
    DateTimeOffset? ExpiresAt, DateTimeOffset UpdatedAt, DateTimeOffset? ContentPurgedAt, bool RecipientAvailable, string? Text,
    bool CanResend, string? ResendBlockedBy, IReadOnlyList<MailAttemptDto> Attempts);

internal sealed class GetMailMessage(AppDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", (Guid id, GetMailMessage handler, CancellationToken ct) => handler.HandleAsync(id, ct).ToHttpResultAsync())
        .Audited(MailAudit.MessageRead)
        .WithName("GetMailMessage");

    public async Task<Result<MailMessageDetailDto>> HandleAsync(Guid id, CancellationToken ct)
    {
        var message = await db.Set<OutboundMessage>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (message is null) return MailErrors.MessageNotFound;
        var requester = message.RequestedById is { } requestedBy ? await db.Users.AsNoTracking().Where(x => x.Id == requestedBy).Select(x => x.DisplayName).SingleOrDefaultAsync(ct) : null;
        var content = message.Sensitive ? null : await db.Set<OutboundMessageContent>().AsNoTracking().Where(x => x.MessageId == id && !x.Protected)
            .Select(x => x.Text).SingleOrDefaultAsync(ct);
        var attempts = await db.Set<DeliveryAttempt>().AsNoTracking().Where(x => x.MessageId == id).OrderBy(x => x.Attempt)
            .Select(x => new MailAttemptDto(x.Attempt, x.Transport, x.StartedAt, x.FinishedAt, x.Outcome, x.SmtpStatus, x.EnhancedStatus, x.Response, x.ErrorCode, x.IssueCode, x.Stage))
            .ToListAsync(ct);
        var blocked = ResendMailMessage.Blocker(message, content is not null)?.Code;
        return new MailMessageDetailDto(MailMessageDto.From(message), message.HeaderMessageId, message.FromAddress, message.BatchId, message.ResendOfId,
            message.RequestedById, requester, message.RecipientUserId, message.Transport, message.ProviderResponse, message.IssueCode, message.TraceId,
            message.ExpiresAt, message.UpdatedAt, message.ContentPurgedAt, message.RecipientAddress is not null, content, blocked is null, blocked, attempts);
    }
}
