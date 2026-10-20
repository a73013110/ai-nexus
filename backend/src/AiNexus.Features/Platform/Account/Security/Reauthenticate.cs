using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Platform.Account.Security;

internal sealed class ReauthenticationRequestValidator : RequestValidator<ReauthenticationRequest>
{
    public override string ProblemCode => "invalid_reauthentication";

    public ReauthenticationRequestValidator()
    {
        // 擇一：密碼，或驗證碼 Id＋驗證碼；兩者都給或都沒給都是請求格式錯誤，不是驗證失敗。
        RuleFor(x => x).Must(x => (x.Password is { Length: > 0 }) != (x.ChallengeId is not null && x.Code is { Length: > 0 })).WithErrorCode("one_method");
        RuleFor(x => x.Password).MaximumLength(1024);
        RuleFor(x => x.Code).MaximumLength(16);
    }
}

/// <summary>
/// 步升驗證：以目前的密碼或寄到信箱的驗證碼確認本人，成功後 <see cref="SelfServiceOptions.ReauthenticationMinutes"/> 內可做敏感變更。
/// 重發 cookie 後 claims 改變，回傳新的 CSRF token。成功與失敗都寫稽核（不含密碼與驗證碼）。
/// </summary>
internal sealed class Reauthenticate(AppDbContext db, AccountSecurity security, Reauthentication reauthentication, VerificationChallenges challenges)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/settings/account/reauthentications", (ReauthenticationRequest body, HttpContext http, ICurrentUser user, Reauthenticate handler, CancellationToken ct)
            => handler.HandleAsync(http, user.User, body, ct).ToHttpResultAsync())
        .RequireRateLimiting(AccountModule.VerificationRateLimit)
        .WithName("Reauthenticate");

    public async Task<Result<AccountSecurityChangeDto>> HandleAsync(HttpContext http, User user, ReauthenticationRequest body, CancellationToken ct)
    {
        var allowed = Reauthentication.Allowed(http);
        if (!allowed.IsSuccess) return allowed.Error;
        var kind = body.Password is not null ? "password" : "code";
        var verified = body.Password is not null
            ? await reauthentication.VerifyPasswordAsync(http, user, body.Password, ct)
            : await VerifyCodeAsync(user, body.ChallengeId!.Value, body.Code, ct);
        security.Audit(user, "account.reauthentication", new { kind }, verified.IsSuccess ? "success" : "failed");
        await db.SaveChangesAsync(ct);
        if (!verified.IsSuccess) return verified.Error;
        await reauthentication.MarkAsync(http, user);
        return security.Changed(http, user);
    }

    private async Task<Result> VerifyCodeAsync(User user, Guid challengeId, string? code, CancellationToken ct)
    {
        var verified = await challenges.VerifyAsync(user.Id, challengeId, VerificationPurposes.Reauthentication, code, ct);
        return verified.IsSuccess ? Result.Success : verified.Error;
    }
}
