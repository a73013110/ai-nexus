using AiNexus.Platform.Validation;

namespace AiNexus.Features.Platform.Account.Security;

/// <summary>本地登入（帳號名＋密碼）。<paramref name="Required"/> 為 true 表示本地是唯一的登入方式，不能停用。</summary>
public sealed record LocalLoginDto(bool Allowed, bool Enabled, string? Account, DateTimeOffset? PasswordChangedAt, bool Required);

/// <summary>
/// 設定 → 帳號與權限的安全資料。<paramref name="ReauthMethods"/> 是可用的步升方式（<c>password</c>、<c>code</c>）；
/// <paramref name="ReauthenticationRequired"/> 為 true 時，敏感變更前要先步升（Windows 整合驗證的請求不需要）。
/// </summary>
public sealed record AccountSecurityDto(string DisplayName, string Account, string? AdAccount, string? Email, string? EmailSource, DateTimeOffset? EmailVerifiedAt,
    bool EmailEditable, LocalLoginDto LocalLogin, string CurrentMethod, IReadOnlyList<string> ReauthMethods, bool ReauthenticationRequired,
    DateTimeOffset? ReauthenticatedUntil, bool MailAvailable, bool ReadOnly);

/// <summary>
/// 會改變 session 的操作的回應。claims 改變後舊的 CSRF token 失效，前端要換成 <paramref name="CsrfToken"/>；
/// <paramref name="SignedOut"/> 為 true 表示目前的本地登入已停用、這個 session 已登出。
/// </summary>
public sealed record AccountSecurityChangeDto(AccountSecurityDto Security, string? CsrfToken, bool SignedOut = false);

/// <summary>已寄出的驗證碼；<paramref name="Delivery"/> 是信件狀態（queued、sending、sent、failed、cancelled，查不到時為 unknown）。</summary>
public sealed record VerificationCodeDto(Guid Id, string Purpose, DateTimeOffset ExpiresAt, int AttemptsLeft, bool Open, string Delivery, string? MaskedTarget);

public sealed record VerificationCodeRequest(string Purpose);

/// <summary>擇一：目前的密碼，或寄到信箱的驗證碼（<paramref name="ChallengeId"/>＋<paramref name="Code"/>）。</summary>
public sealed record ReauthenticationRequest(string? Password = null, Guid? ChallengeId = null, string? Code = null);

[ValidatedInHandler("UserCredentials.CheckEmailAsync answers email_invalid, email_in_use or email_managed_by_directory with its own codes.")]
public sealed record EmailChangeRequest(string Email);

[ValidatedInHandler("A malformed code counts as a wrong attempt in VerificationChallenges.VerifyAsync, like any other wrong code.")]
public sealed record VerificationCodeConfirmation(string Code);

/// <summary><paramref name="Account"/> 空白時沿用目前的本地帳號。</summary>
[ValidatedInHandler("UserCredentials.SetLocalLoginAsync answers invalid_account, invalid_password, password_weak or account_exists with its own codes.")]
public sealed record LocalLoginRequest(string? Account, string Password);
