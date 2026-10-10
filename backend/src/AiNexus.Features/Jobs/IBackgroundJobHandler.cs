using AiNexus.Platform.Errors;

namespace AiNexus.Features.Jobs;

/// <summary>
/// Runs one kind of job. An expected failure is returned and recorded as the job's error code; an exception (an external
/// service, the database) fails the job as well, with its code when it is an <see cref="ExternalServiceException"/>.
/// </summary>
public interface IBackgroundJobHandler
{
    string Kind { get; }
    Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct);

    /// <summary>Whether a failed or cancelled job may run again; its error answers the retry request.</summary>
    Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct);
}
