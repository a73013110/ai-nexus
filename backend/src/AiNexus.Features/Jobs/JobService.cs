using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using System.Diagnostics;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Jobs;

/// <summary>其他模組使用背景工作的入口：以業務對象排入工作、取得描述、取消與重試。</summary>
public sealed class JobService(AppDbContext db, IServiceProvider services)
{
    public static JobDto Describe(BackgroundJob x) => new(
        x.Id,
        x.Kind,
        x.SubjectId,
        x.Label,
        x.Status,
        x.Stage,
        x.Attempt,
        x.CompletedUnits,
        x.TotalUnits,
        x.CancelRequested,
        x.ErrorCode,
        x.Status == "failed" ? x.ErrorMessage ?? Issues.Message(x.IssueCode) : null,
        x.CreatedAt,
        x.UpdatedAt,
        x.IssueCode);

    /// <summary>
    /// 排入工作。有 <c>Activity.Current</c> 時沿用其 trace，讓工作與發起的請求串在同一條追蹤上；沒有時才另建一組。
    /// 交易由呼叫端提交，讓業務對象與工作原子地一起寫入。
    /// </summary>
    public BackgroundJob Enqueue(Guid owner, Guid? resource, Guid subject, string kind, string label)
    {
        var job = new BackgroundJob
        {
            OwnerId = owner,
            ResourceId = resource,
            SubjectId = subject,
            Kind = kind,
            Label = label,
            ActiveKey = kind + ":" + subject.ToString("N"),
        };
        job.TraceId = Activity.Current?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString();
        job.ParentSpanId = Activity.Current?.SpanId.ToHexString() ?? ActivitySpanId.CreateRandom().ToHexString();
        job.OperationId = job.Id;
        db.Add(job);
        return job;
    }

    /// <summary>取消工作，委派給 <see cref="CancelJob"/>。</summary>
    public Task<Result<JobDto>> CancelAsync(Guid owner, Guid id, CancellationToken ct) =>
        services.GetRequiredService<CancelJob>().HandleAsync(owner, id, ct);

    /// <summary>重試工作，委派給 <see cref="RetryJob"/>。</summary>
    public Task<Result<JobDto>> RetryAsync(Guid owner, Guid id, CancellationToken ct) =>
        services.GetRequiredService<RetryJob>().HandleAsync(owner, id, ct);
}
