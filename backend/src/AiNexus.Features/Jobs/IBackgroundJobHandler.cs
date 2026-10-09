namespace AiNexus.Features.Jobs;

public interface IBackgroundJobHandler
{
    string Kind { get; }
    Task ExecuteAsync(JobExecution execution, CancellationToken ct);
    Task ValidateRetryAsync(BackgroundJob job, CancellationToken ct);
}
