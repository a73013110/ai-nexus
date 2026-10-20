using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Platform.Account.Security;

/// <summary>
/// 輸入寄到新信箱的驗證碼，完成信箱設定：寫入本人驗證的信箱、遞增 SecurityVersion（其他工作階段登出）並重發目前的 session。
/// 原本有另一個已驗證的信箱時，寄通知到舊信箱（只含遮罩後的新地址）。
/// </summary>
internal sealed class ConfirmEmailChange(AccountSecurity security, UserCredentials credentials, VerificationChallenges challenges)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/settings/account/email-changes/{id:guid}/confirmation", (Guid id, VerificationCodeConfirmation body, HttpContext http, ICurrentUser user, ConfirmEmailChange handler, CancellationToken ct)
            => handler.HandleAsync(http, user.User, id, body, ct).ToHttpResultAsync())
        .RequireRateLimiting(AccountModule.VerificationRateLimit)
        .WithName("ConfirmEmailChange");

    public async Task<Result<AccountSecurityChangeDto>> HandleAsync(HttpContext http, User user, Guid id, VerificationCodeConfirmation body, CancellationToken ct)
    {
        var allowed = Reauthentication.Allowed(http);
        if (!allowed.IsSuccess) return allowed.Error;
        var challenge = await challenges.VerifyAsync(user.Id, id, VerificationPurposes.EmailChange, body.Code, ct);
        if (!challenge.IsSuccess) return challenge.Error;
        // 寄出驗證碼後，這個地址可能已被他人驗證，或信箱改由目錄提供；以確認當下的狀態為準。
        var address = await credentials.CheckEmailAsync(user, challenge.Value.Target, ct);
        if (!address.IsSuccess) return address.Error;
        var previous = AccountSecurity.VerifiedEmail(user);
        credentials.SetVerifiedEmail(user, address.Value);
        security.Audit(user, "account.email_changed", new { from = previous is null ? null : UserEmails.Mask(previous), to = UserEmails.Mask(address.Value) });
        if (previous is not null && !UserEmails.Same(previous, address.Value))
            await security.NotifyAsync(user, previous, "變更了通知信箱", $"新的通知信箱：{UserEmails.Mask(address.Value)}", ct);
        return await security.CommitAsync(http, user, ct);
    }
}
