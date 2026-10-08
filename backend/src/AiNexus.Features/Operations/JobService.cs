using System.Diagnostics;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Operations;

/// <summary>Background jobs as other modules use them: enqueue with their subject, describe, cancel and retry.</summary>
public sealed class JobService(NexusDbContext db, IServiceProvider services)
{
    public static JobDto Describe(BackgroundJob x) => new(x.Id, x.Kind, x.SubjectId, x.Label, x.Status, x.Stage, x.Attempt, x.CompletedUnits, x.TotalUnits, x.CancelRequested, x.ErrorCode, x.Status == "failed" ? Issues.Message(x.IssueCode) : null, x.CreatedAt, x.UpdatedAt, x.IssueCode);
    public BackgroundJob Enqueue(Guid owner, Guid? resource, Guid subject, string kind, string label)
    {
        var job = new BackgroundJob { OwnerId = owner, ResourceId = resource, SubjectId = subject, Kind = kind, Label = label, ActiveKey = kind + ":" + subject.ToString("N") };
        job.TraceId = Activity.Current?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString(); job.ParentSpanId = Activity.Current?.SpanId.ToHexString() ?? ActivitySpanId.CreateRandom().ToHexString(); job.OperationId = job.Id;
        db.Add(job); return job; // The caller commits subject and job atomically.
    }

    /// <summary>See <see cref="CancelJob"/>. Expected failures are exceptions for callers in other modules.</summary>
    public async Task<JobDto> CancelAsync(Guid owner, Guid id, CancellationToken ct) => Unwrap(await services.GetRequiredService<CancelJob>().HandleAsync(owner, id, ct));

    /// <summary>See <see cref="RetryJob"/>. Expected failures are exceptions for callers in other modules.</summary>
    public async Task<JobDto> RetryAsync(Guid owner, Guid id, CancellationToken ct) => Unwrap(await services.GetRequiredService<RetryJob>().HandleAsync(owner, id, ct));

    private static JobDto Unwrap(Result<JobDto> result) => result.IsSuccess ? result.Value : throw result.Error.ToException();
}
