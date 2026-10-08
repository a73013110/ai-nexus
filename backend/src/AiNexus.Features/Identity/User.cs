using AiNexus.Platform.Errors;

namespace AiNexus.Features.Identity;

public sealed class NexusUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sid { get; set; } = "";
    public string Account { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public long? AttachmentLimitBytes { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool AdEnabled { get; set; } = true;
    public bool LocalEnabled { get; set; }
    public string? AdAccount { get; set; }
    public string? LocalAccount { get; set; }
    public string? PasswordHash { get; set; }
    public int SecurityVersion { get; set; }
    public bool ProfileManaged { get; set; }
    public int FailedLogins { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public UserPreferences Preferences { get; set; } = new();
}

public sealed class UserPreferences
{
    public const int DefaultReadingFontSize = 15;
    public const double DefaultReadingLineHeight = 1.2;
    public const int DefaultSidebarWidth = 240;
    public string Theme { get; set; } = "system";
    public bool ReducedMotion { get; set; }
    public string? DefaultModelId { get; set; }
    public int ReadingFontSize { get; set; } = DefaultReadingFontSize;
    public double ReadingLineHeight { get; set; } = DefaultReadingLineHeight;
    public string Density { get; set; } = "comfortable";
    public int SidebarWidth { get; set; } = DefaultSidebarWidth;
    public string ReadingWidth { get; set; } = "standard";
    public bool EnterToSend { get; set; } = true;
    public bool AutoFollow { get; set; } = true;
    public bool SaveLocalDrafts { get; set; } = true;
    public bool NotifyOnCompletion { get; set; }
    public string DefaultReasoningEffort { get; set; } = "auto";
}

internal static class IdentityErrors
{
    public static readonly Error InvalidTheme = Error.Invalid("invalid_theme");
    public static readonly Error InvalidModel = Error.Invalid("invalid_model");
    public static readonly Error ModelNotAllowed = Error.Invalid("model_not_allowed");
    public static readonly Error WindowsModeRequired = Error.Invalid("authentication_mode");
    public static readonly Error TestIdentityNested = Error.Conflict("test_identity_nested");
    public static readonly Error TestReasonRequired = Error.Invalid("test_reason_required");
    public static readonly Error AdminRequired = Error.Forbidden("admin_required");
    public static readonly Error TestUserNotFound = Error.NotFound("user_not_found");
    public static readonly Error TestIdentitySame = Error.Invalid("test_identity_same");
    public static readonly Error TestSourceRevoked = new(ErrorKind.Unauthenticated, "test_source_revoked");
    public static readonly Error TestSessionInvalid = new(ErrorKind.Unauthenticated, "test_session_invalid");
}
