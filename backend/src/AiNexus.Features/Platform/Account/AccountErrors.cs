using AiNexus.Platform.Errors;

namespace AiNexus.Features.Platform.Account;

internal static class AccountErrors
{
    public static readonly Error InvalidTheme = Error.Invalid("invalid_theme");
    public static readonly Error InvalidModel = Error.Invalid("invalid_model");
    public static readonly Error ModelNotAllowed = Error.Invalid("model_not_allowed");
    /// <summary>步升驗證碼要寄到已驗證的信箱；沒有時只能用密碼。</summary>
    public static readonly Error EmailNotVerified = Error.Conflict("email_not_verified");
    /// <summary>帳號安全設定在同一時間被其他請求改過（SecurityVersion 並行衝突）。</summary>
    public static readonly Error AccountChanged = Error.Conflict("account_changed");
    public static readonly Error VerificationNotFound = Error.NotFound("verification_not_found");
}
