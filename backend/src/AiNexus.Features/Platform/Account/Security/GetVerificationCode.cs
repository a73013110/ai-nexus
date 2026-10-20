using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Features.Admin.Mail;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Platform.Account.Security;

/// <summary>驗證碼的到期時間、剩餘次數與寄送狀態，讓使用者知道信是否已寄出。只看得到自己的驗證碼。</summary>
internal sealed class GetVerificationCode(VerificationChallenges challenges, MailOutbox outbox)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/settings/account/verification-codes/{id:guid}", (Guid id, ICurrentUser user, GetVerificationCode handler, CancellationToken ct)
            => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("GetVerificationCode");

    public async Task<Result<VerificationCodeDto>> HandleAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var challenge = await challenges.FindAsync(userId, id, ct);
        if (challenge is null) return AccountErrors.VerificationNotFound;
        var delivery = challenge.MailMessageId is { } message ? await outbox.GetDeliveryAsync(message, ct) : null;
        return new VerificationCodeDto(challenge.Id, challenge.Purpose, challenge.ExpiresAt, challenges.AttemptsLeft(challenge), challenges.Open(challenge),
            AccountSecurity.Delivery(delivery), challenge.Target is { } target ? UserEmails.Mask(target) : null);
    }
}
