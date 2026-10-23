using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Identity;

internal static class IdentityErrors
{
    public static readonly Error AuthenticationRequired = Error.Unauthenticated("authentication_required");
    public static readonly Error SessionRevoked = Error.Unauthenticated("session_revoked");
    public static readonly Error WindowsSidRequired = Error.Forbidden("windows_sid_required");
    public static readonly Error WindowsAccountRequired = Error.Forbidden("windows_account_required");
    public static readonly Error LoginMethodDisabled = Error.Forbidden("login_method_disabled");
    public static readonly Error AdBindingConflict = Error.Forbidden("ad_binding_conflict");
    public static readonly Error InvalidAccount = Error.Invalid("invalid_account");
    public static readonly Error InvalidCredentials = Error.Unauthenticated("invalid_credentials");
    public static readonly Error AdInvalidCredentials = Error.Unauthenticated("ad_invalid_credentials");
    public static readonly Error AdSidRequired = Error.Forbidden("ad_sid_required");
    public static readonly Error AuthenticationModeUnsupported = Error.Invalid("authentication_mode");
    public static readonly Error TestAdminReadOnly = Error.Forbidden("test_admin_read_only");
    public static readonly Error IdentityChanged = Error.Conflict("identity_changed");
    public static readonly Error TestIdentityNested = Error.Conflict("test_identity_nested");
    public static readonly Error TestReasonRequired = Error.Invalid("test_reason_required");
    public static readonly Error TestIdentitySame = Error.Invalid("test_identity_same");
    public static readonly Error TestSourceRevoked = Error.Unauthenticated("test_source_revoked");
    public static readonly Error IdentityNotFound = Error.NotFound("identity_not_found");
    public static readonly Error TestSessionInvalid = Error.Unauthenticated("test_session_invalid");
    public static readonly Error AdminResourceNotFound = Error.NotFound("admin_resource_not_found");
    public static readonly Error InvalidUser = Error.Invalid("invalid_user");
    public static readonly Error InvalidLoginMethods = Error.Invalid("invalid_login_methods");
    public static readonly Error InvalidPassword = Error.Invalid("invalid_password");
    public static readonly Error LocalPasswordRequired = Error.Invalid("local_password_required");
    public static readonly Error InvalidRoles = Error.Invalid("invalid_roles");
    public static readonly Error UserNotFound = Error.NotFound("user_not_found");
    public static readonly Error AccountExists = Error.Conflict("account_exists");
    public static readonly Error AdBindingImmutable = Error.Conflict("ad_binding_immutable");
    public static readonly Error DirectoryVerificationRequired = Error.Forbidden("directory_verification_required");
    public static readonly Error ReauthenticationRequired = Error.Forbidden("reauthentication_required");
    public static readonly Error ReauthenticationUnavailable = Error.Conflict("reauthentication_unavailable");
    // 步升驗證的密碼錯誤不能用 401：前端會把 401 當成工作階段失效而登出。
    public static readonly Error ReauthenticationFailed = Error.Invalid("reauthentication_failed");
    public static readonly Error VerificationCodeInvalid = Error.Invalid("verification_code_invalid");
    public static readonly Error VerificationExpired = Error.Conflict("verification_expired");
    public static readonly Error VerificationRateLimited = Error.RateLimited("verification_rate_limited");
    public static readonly Error VerificationCooldown = Error.RateLimited("verification_cooldown");
    public static readonly Error EmailInvalid = Error.Invalid("email_invalid");
    public static readonly Error EmailManagedByDirectory = Error.Conflict("email_managed_by_directory");
    public static readonly Error EmailInUse = Error.Conflict("email_in_use");
    public static readonly Error LocalLoginUnavailable = Error.Forbidden("local_login_unavailable");
    public static readonly Error LocalLoginRequired = Error.Conflict("local_login_required");
    public static readonly Error PasswordWeak = Error.Invalid("password_weak");
    // 目錄查詢（匯入、從 AD 更新）的失敗不沿用登入的 ad_* 代碼：管理頁要能提示「服務帳號未設定」與「暫時連不上」，登入則刻意不分辨。
    public static readonly Error DirectoryNotConfigured = Error.Unavailable("directory_not_configured");
    public static readonly Error DirectoryUnavailable = Error.Unavailable("directory_unavailable");
    // 目錄樹的節點：DN 不在搜尋根之下（或格式不合），以及畫面上的樹已過時、節點在 AD 已刪除或改名。
    public static readonly Error DirectoryNodeInvalid = Error.Invalid("directory_node_invalid");
    public static readonly Error DirectoryNodeNotFound = Error.NotFound("directory_node_not_found");
    // 檢視單一人員時 SID 已不在目錄（刪除或移出搜尋根），格式不合的 SID 也一樣回報。
    public static readonly Error DirectoryPersonNotFound = Error.NotFound("directory_person_not_found");
    public static readonly Error DirectoryQueryInvalid = Error.Invalid("directory_query_invalid");
    // 預覽之後、寫入之前資料已被別人改動（同時登入、另一位管理員編輯）：該人不寫入，請重新預覽。
    public static readonly Error DirectoryChanged = Error.Conflict("directory_changed");
}
