using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Mail.Messages;

/// <summary>
/// 以原信渲染好的內容建立一封新信（<c>ResendOfId</c> 指向原信），原紀錄不變；寫行政稽核 <c>admin.mail_resend</c>。
/// 敏感信、內文或收件原址已清除時不能重寄。<c>Idempotency-Key</c>（GUID）讓連點或重試只建立一封。
/// </summary>
internal sealed class ResendMailMessage(AppDbContext db, MailOutbox outbox, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/resend", (Guid id, [FromHeader(Name = "Idempotency-Key")] Guid? requestKey, IRequestActor actor, ResendMailMessage handler, CancellationToken ct) =>
            handler.HandleAsync(actor.Id, id, requestKey, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.MailMessagesResend)
        .WithName("ResendMailMessage");

    public async Task<Result<MailEnqueuedDto>> HandleAsync(Guid actor, Guid id, Guid? requestKey, CancellationToken ct)
    {
        MailEnqueuedDto? result = null;
        var outcome = await audit.MutateAsync(actor, FeatureCatalog.MailMessagesResend, "admin.mail_resend", id, id.ToString(),
            token => Snapshot(result?.MessageId, token), async () =>
            {
                var original = await db.Set<OutboundMessage>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
                if (original is null) return MailErrors.MessageNotFound;
                var content = await db.Set<OutboundMessageContent>().AsNoTracking().SingleOrDefaultAsync(x => x.MessageId == id && !x.Protected, ct);
                if (Blocker(original, content is not null) is { } blocked) return blocked;
                var key = requestKey is { } request ? $"mail.resend:{id:N}:{request:N}" : null;
                if (await outbox.ExistingAsync(key, ct) is { } existing) { result = new(existing, true); return Result.Success; }
                var copy = outbox.AddCopy(original, content!, original.RecipientAddress!, new(IdempotencyKey: key, RequestedBy: actor));
                result = new(copy.Id, false);
                return Result.Success;
            }, ct);
        return outcome.IsSuccess ? result! : outcome.Error!;
    }

    /// <summary>不能重寄的原因；詳情頁也用它決定是否顯示重寄按鈕。</summary>
    internal static Error? Blocker(OutboundMessage message, bool hasContent)
        => message.Sensitive || !hasContent ? MailErrors.ContentUnavailable
            : message.RecipientAddress is null ? MailErrors.RecipientUnavailable
            : null;

    // 稽核只記新信的 ID、狀態與分類，不記收件者與內容。
    private async Task<object?> Snapshot(Guid? created, CancellationToken ct)
        => created is { } id ? await db.Set<OutboundMessage>().AsNoTracking().Where(x => x.Id == id).Select(x => new { id = x.Id, status = x.Status, category = x.Category }).SingleOrDefaultAsync(ct) : null;
}
