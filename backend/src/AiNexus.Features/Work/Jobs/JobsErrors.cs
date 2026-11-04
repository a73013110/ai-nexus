using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Jobs;

internal static class JobsErrors
{
    public static readonly Error JobNotFound = Error.NotFound("job_not_found");
    public static readonly Error JobNotRetryable = Error.Conflict("job_not_retryable");
    /// <summary>工作多次在執行中中斷，已停止自動接手；使用者仍可重試。</summary>
    public static readonly Error JobRecoveryLimit = Error.Conflict("job_recovery_limit");
    public static readonly Error JobHandlerMissing = Error.Conflict("job_handler_missing");
    public static readonly Error JobActive = Error.Conflict("job_active");
    public static readonly Error JobChanged = Error.Conflict("job_changed");
    /// <summary>處理器拋出非外部服務的例外。</summary>
    public static readonly Error JobProcessingFailed = Error.Unavailable("job_processing_failed");
}
