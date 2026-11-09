using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Jobs;
using AiNexus.Platform.Modules;
using System.Diagnostics;
using AiNexus.Platform.Diagnostics;
using AiNexus.Features.Work.Jobs.Management;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Jobs;

/// <summary>其他模組使用背景工作的入口：以業務對象排入工作、取得描述、取消與重試。</summary>
[Scoped]
public sealed class JobService(AppDbContext db, IServiceProvider services, JobQueueSignal signal) : IDisposable
{
    private bool enqueued;

    /// <summary>
    /// 工作要等呼叫端提交才看得到，所以等範圍結束（請求或背景工作收尾、交易已提交）才叫醒 worker。
    /// 呼叫端回滾時多叫醒一次，worker 查不到工作就繼續睡，沒有副作用。
    /// </summary>
    public void Dispose()
    {
        if (enqueued) signal.Wake();
    }

    /// <summary>
    /// 自動接手的嘗試上限：租約到期（前一個 worker 中止）時，總嘗試次數已超過它就不再執行而直接失敗，
    /// 避免讓程序中止的工作無限循環。使用者的重試是明確決定，不受此限。
    /// </summary>
    internal const int MaxRecoveredAttempts = 6;

    /// <summary>認領、heartbeat 與檢查點每次把租約延長的秒數；heartbeat 間隔必須遠小於它。</summary>
    internal const int LeaseSeconds = 60;

    /// <summary>同一業務對象同一種工作只能有一筆進行中，由資料庫的 filtered unique index 擋下重複。</summary>
    internal static string ActiveKey(string kind, Guid subject) => kind + ":" + subject.ToString("N");

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
        x.Status == RunStates.Failed ? x.ErrorMessage ?? Issues.Message(x.IssueCode) : null,
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
            ActiveKey = ActiveKey(kind, subject),
        };
        job.TraceId = Activity.Current?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString();
        job.ParentSpanId = Activity.Current?.SpanId.ToHexString() ?? ActivitySpanId.CreateRandom().ToHexString();
        job.OperationId = job.Id;
        db.Add(job);
        enqueued = true;
        return job;
    }

    /// <summary>取消工作，委派給 <see cref="CancelJob"/>。</summary>
    public Task<Result<JobDto>> CancelAsync(Guid owner, Guid id, CancellationToken ct) =>
        services.GetRequiredService<CancelJob>().HandleAsync(owner, id, ct);

    /// <summary>重試工作，委派給 <see cref="RetryJob"/>。</summary>
    public Task<Result<JobDto>> RetryAsync(Guid owner, Guid id, CancellationToken ct) =>
        services.GetRequiredService<RetryJob>().HandleAsync(owner, id, ct);
}
