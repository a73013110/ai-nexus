using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Features.Admin.Mail;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Account.Security;

internal sealed class VerificationCodeRequestValidator : RequestValidator<VerificationCodeRequest>
{
    public override string ProblemCode => "invalid_verification_request";

    // 信箱變更的驗證碼由 RequestEmailChange 寄到新地址；這裡只寄步升驗證碼到已驗證的信箱。
    public VerificationCodeRequestValidator() => RuleFor(x => x.Purpose).Equal(VerificationPurposes.Reauthentication).WithErrorCode("unknown");
}

/// <summary>寄步升驗證碼到本人已驗證的信箱（沒有密碼可用、或想改用驗證碼時）。</summary>
internal sealed class SendVerificationCode(VerificationChallenges challenges, MailOutbox outbox, IOptions<SelfServiceOptions> options)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/settings/account/verification-codes", (VerificationCodeRequest body, HttpContext http, ICurrentUser user, SendVerificationCode handler, CancellationToken ct)
            => handler.HandleAsync(http, user.User, ct).ToHttpResultAsync())
        .RequireRateLimiting(AccountModule.VerificationRateLimit)
        .Audited(AccountAudit.VerificationCodeSent)
        .WithName("SendVerificationCode");

    public async Task<Result<VerificationCodeDto>> HandleAsync(HttpContext http, User user, CancellationToken ct)
    {
        var allowed = Reauthentication.Allowed(http);
        if (!allowed.IsSuccess) return allowed.Error;
        if (!outbox.Available) return MailErrors.NotConfigured;
        if (AccountSecurity.VerifiedEmail(user) is not { } address) return AccountErrors.EmailNotVerified;
        return await IssueAsync(challenges, outbox, user, VerificationPurposes.Reauthentication, address, new ReauthenticationCodeMail(), options.Value.CodeMinutes, ct);
    }

    /// <summary>發碼並在同一交易入列驗證碼信；新碼取代舊碼時取消還沒寄出的舊信。</summary>
    internal static async Task<Result<VerificationCodeDto>> IssueAsync(VerificationChallenges challenges, MailOutbox outbox, User user,
        string purpose, string address, VerificationCodeMail mail, int minutes, CancellationToken ct)
    {
        var issued = await challenges.IssueAsync(user.Id, purpose, purpose == VerificationPurposes.EmailChange ? address : null, ct);
        if (!issued.IsSuccess) return issued.Error;
        foreach (var superseded in issued.Value.Superseded) await outbox.CancelAsync(superseded, ct);
        var challenge = issued.Value.Challenge;
        var enqueued = await outbox.EnqueueAsync(mail, new VerificationCodeMailModel(user.DisplayName, issued.Value.Code, minutes), new MailRecipient(address, user.DisplayName, user.Id),
            new MailEnqueueOptions($"{mail.Category}:{challenge.Id:N}", challenge.ExpiresAt, user.Id), ct);
        if (!enqueued.IsSuccess) return enqueued.Error;
        challenge.MailMessageId = enqueued.Value.MessageId;
        var saved = await challenges.SaveAsync(ct);
        if (!saved.IsSuccess) return saved.Error;
        return new VerificationCodeDto(challenge.Id, purpose, challenge.ExpiresAt, challenges.AttemptsLeft(challenge), true, "queued", UserEmails.Mask(address));
    }
}
