using AiNexus.Platform.Diagnostics;
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

    /// <summary>For callers that can only fail by exception, such as background jobs, which record the error code.</summary>
    public static ApiException ToException(this Error error)
    {
        var status = Problems.Status(error.Kind);
        return new(status, error.Code, PublicErrorCatalog.Message(error.Code, status));
    }

    public static T OrThrow<T>(this Result<T> result) => result.IsSuccess ? result.Value : throw result.Error.ToException();

    public static void OrThrow(this Result result)
    {
        if (!result.IsSuccess) throw result.Error.ToException();
    }
}
