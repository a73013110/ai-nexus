using AiNexus.Platform.Errors;

namespace AiNexus.Features.Identity;

internal static class IdentityErrors
{
    public static readonly Error WindowsModeRequired = Error.Invalid("authentication_mode");
    public static readonly Error TestIdentityNested = Error.Conflict("test_identity_nested");
    public static readonly Error TestReasonRequired = Error.Invalid("test_reason_required");
    public static readonly Error AdminRequired = Error.Forbidden("admin_required");
    public static readonly Error TestUserNotFound = Error.NotFound("user_not_found");
    public static readonly Error TestIdentitySame = Error.Invalid("test_identity_same");
    public static readonly Error TestSourceRevoked = new(ErrorKind.Unauthenticated, "test_source_revoked");
    public static readonly Error TestSessionInvalid = new(ErrorKind.Unauthenticated, "test_session_invalid");
}
