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
}
