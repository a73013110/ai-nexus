using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Features.Admin.Mail;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Account.Security;

/// <summary>
/// 設定或變更通知信箱的第一步（需步升）：把驗證碼寄到<b>新地址</b>，證明使用者收得到。輸入目前尚未驗證的信箱就是「驗證目前信箱」。
/// 地址綁在驗證碼上，確認時不接受請求再帶一次。
/// </summary>
internal sealed class RequestEmailChange(Reauthentication reauthentication, UserCredentials credentials, VerificationChallenges challenges, MailOutbox outbox,
    IOptions<SelfServiceOptions> options)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/settings/account/email-changes", (EmailChangeRequest body, HttpContext http, ICurrentUser user, RequestEmailChange handler, CancellationToken ct)
            => handler.HandleAsync(http, user.User, body, ct).ToHttpResultAsync())
        .RequireRateLimiting(AccountModule.VerificationRateLimit)
        .WithName("RequestEmailChange");

    public async Task<Result<VerificationCodeDto>> HandleAsync(HttpContext http, User user, EmailChangeRequest body, CancellationToken ct)
    {
        var fresh = reauthentication.Require(http);
        if (!fresh.IsSuccess) return fresh.Error;
        if (!outbox.Available) return MailErrors.NotConfigured;
        var address = await credentials.CheckEmailAsync(user, body.Email, ct);
        if (!address.IsSuccess) return address.Error;
        return await SendVerificationCode.IssueAsync(challenges, outbox, user, VerificationPurposes.EmailChange, address.Value, new EmailChangeCodeMail(), options.Value.CodeMinutes, ct);
    }
}
