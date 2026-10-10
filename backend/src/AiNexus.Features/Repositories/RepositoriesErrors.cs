using AiNexus.Platform.Errors;

namespace AiNexus.Features.Repositories;

internal static class RepositoriesErrors
{
    public static readonly Error Disabled = Error.Unavailable("gitea_disabled");
    public static readonly Error NotConnected = Error.Conflict("gitea_not_connected");
    public static readonly Error ReconnectRequired = Error.Conflict("gitea_reconnect_required");
    public static readonly Error DiffUnsupported = Error.Conflict("gitea_diff_unsupported");
    public static readonly Error InvalidRepository = Error.Invalid("invalid_repository");
    public static readonly Error InvalidCommit = Error.Invalid("invalid_commit");
    public static readonly Error InvalidPath = Error.Invalid("invalid_repository_path");
    public static readonly Error InvalidPage = Error.Invalid("invalid_page");
    public static readonly Error Empty = Error.Conflict("repository_empty");
    public static readonly Error NotDirectory = Error.Invalid("repository_not_directory");
    public static readonly Error FileLimit = Error.TooLarge("repository_file_limit");
    public static readonly Error NotText = Error.Invalid("repository_not_text");
    public static readonly Error ReviewNotFound = Error.NotFound("review_not_found");
    public static readonly Error ReviewHostChanged = Error.Conflict("review_host_changed");
    public static readonly Error ReviewEmptyRange = Error.Invalid("review_empty_range");
    public static readonly Error ReviewPurposeInvalid = Error.Invalid("review_purpose_invalid");
    public static readonly Error IdempotencyConflict = Error.Conflict("idempotency_conflict");
    public static readonly Error SnapshotUnsupported = Error.Conflict("review_snapshot_unsupported");
    public static readonly Error ReviewNoChanges = Error.Invalid("review_no_changes");
    public static readonly Error DiffLimit = Error.TooLarge("repository_diff_limit");
    public static readonly Error DiffNotText = Error.Invalid("repository_diff_not_text");
    public static readonly Error SegmentLimit = Error.TooLarge("review_segment_limit");
    public static readonly Error ModelContextSmall = Error.Invalid("review_model_context_small");
    public static readonly Error ReviewAccessRevoked = Error.Forbidden("review_access_revoked");
    public static readonly Error ReviewBriefInvalid = Error.Upstream("review_brief_invalid");
    public static readonly Error ReviewSummaryTooLong = Error.Upstream("review_summary_too_long");
    public static readonly Error IdentityInvalid = Error.Upstream("gitea_identity_invalid");
    public static readonly Error ResponseInvalid = Error.Upstream("gitea_response_invalid");
    public static readonly Error Timeout = Error.Timeout("gitea_timeout");
    public static readonly Error Unreachable = Error.Unavailable("gitea_unavailable");
}
