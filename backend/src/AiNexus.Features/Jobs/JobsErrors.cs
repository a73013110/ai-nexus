using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Jobs;

internal static class JobsErrors
{
    public static readonly Error JobNotFound = Error.NotFound("job_not_found");
    public static readonly Error JobNotRetryable = Error.Conflict("job_not_retryable");
    public static readonly Error JobRetryLimit = Error.Conflict("job_retry_limit");
    public static readonly Error JobHandlerMissing = Error.Conflict("job_handler_missing");
    public static readonly Error JobActive = Error.Conflict("job_active");
    public static readonly Error JobChanged = Error.Conflict("job_changed");

    /// <summary>For <see cref="JobService"/>, whose callers in other modules can only fail by exception.</summary>
    public static ApiException ToException(this Error error)
    {
        var status = Problems.Status(error.Kind);
        return new(status, error.Code, PublicErrorCatalog.Message(error.Code, status));
    }
}
