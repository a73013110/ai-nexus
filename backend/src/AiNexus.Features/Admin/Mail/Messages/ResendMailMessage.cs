using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Mail.Messages;

/// <summary>
/// 以原信渲染好的內容建立一封新信（<c>ResendOfId</c> 指向原信），原紀錄不變；寫行政稽核 <c>admin.mail_resend</c>。
/// 敏感信、內文或收件原址已清除時不能重寄。<c>Idempotency-Key</c>（GUID）讓連點或重試只建立一封。
/// </summary>
internal sealed class ResendMailMessage(AppDbContext db, MailOutbox outbox, AdministrativeWrite write) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/resend", (Guid id, [FromHeader(Name = "Idempotency-Key")] Guid? requestKey, IRequestActor actor, ResendMailMessage handler, CancellationToken ct) =>
            handler.HandleAsync(actor.Id, id, requestKey, ct))
        .RequireFeature(FeatureCatalog.MailMessagesResend)
        .Audited(MailAudit.MessageSent)
        .WithName("ResendMailMessage");

    public async Task<Result<MailEnqueuedDto>> HandleAsync(Guid actor, Guid id, Guid? requestKey, CancellationToken ct)
    {
        MailEnqueuedDto? result = null;
        var begun = await write.BeginAsync(AuditTarget.Of(id, token => MailAudit.CreatedMessageAsync(db, result?.MessageId, token)), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var original = await db.OutboundMessages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (original is null) return MailErrors.MessageNotFound;
        var content = await db.OutboundMessageContents.AsNoTracking().SingleOrDefaultAsync(x => x.MessageId == id && !x.Protected, ct);
        if (Blocker(original, content is not null) is { } blocked) return blocked;
        var key = requestKey is { } request ? $"mail.resend:{id:N}:{request:N}" : null;
        // 同一個冪等鍵重送：不再建立，回傳先前那封。同時送出的兩筆由冪等鍵的唯一索引擋下。
        result = await outbox.ExistingAsync(key, ct) is { } existing
            ? new(existing, true)
            : new(outbox.AddCopy(original, content!, original.RecipientAddress!, new(IdempotencyKey: key, RequestedBy: actor)).Id, false);

        return await transaction.CommitAsync(() => result, ct);
    }

    /// <summary>不能重寄的原因；詳情頁也用它決定是否顯示重寄按鈕。</summary>
    internal static Error? Blocker(OutboundMessage message, bool hasContent)
        => message.Sensitive || !hasContent ? MailErrors.ContentUnavailable
            : message.RecipientAddress is null ? MailErrors.RecipientUnavailable
            : null;
}
