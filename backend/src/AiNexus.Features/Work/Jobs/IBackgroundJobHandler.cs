using AiNexus.Data.App.Jobs;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Jobs;

/// <summary>
/// 執行一種工作。預期內的失敗以回傳值表示，並記為工作的錯誤代碼；例外（外部服務、資料庫等）同樣會讓工作失敗，
/// 若為 <see cref="ExternalServiceException"/> 則沿用其代碼。以 <see cref="BackgroundJobHandlers.AddBackgroundJobHandler{THandler}"/> 註冊。
/// </summary>
public interface IBackgroundJobHandler
{
    Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct);

    /// <summary>判斷失敗或已取消的工作能否再執行；這裡回傳的錯誤即為重試請求的回應。</summary>
    Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct);
}

/// <summary>工作的執行通道。不同通道互不等待，長時間的批次工作才不會擋住使用者正在等的工作。</summary>
public enum JobLane
{
    /// <summary>使用者正在等結果的工作；沒有宣告通道（含已沒有處理器）的種類都在這裡。</summary>
    Standard,

    /// <summary>全庫重建、評測這類跑很久、晚一點完成也無妨的批次工作。</summary>
    Bulk,
}

/// <summary>宣告走 <see cref="JobLane.Bulk"/> 的工作種類。</summary>
public sealed record BulkJobKind(string Kind);

public static class BackgroundJobHandlers
{
    /// <summary>
    /// 在擁有工作的模組註冊 <paramref name="kind"/> 的處理器（scoped，每次執行一個範圍）。
    /// 種類寫進 <c>BackgroundJob.Kind</c>，改名會讓排隊中的舊工作找不到處理器；與其他處理器重複時啟動失敗。
    /// 不同通道的工作會同時執行：處理器寫入共用資料時要靠資料庫鎖，不能假設一次只跑一件。
    /// </summary>
    public static IServiceCollection AddBackgroundJobHandler<THandler>(this IServiceCollection services, string kind, JobLane lane = JobLane.Standard) where THandler : class, IBackgroundJobHandler
    {
        if (lane == JobLane.Bulk) services.AddSingleton(new BulkJobKind(kind));
        return services.AddKeyedService<IBackgroundJobHandler, THandler>(kind, ServiceLifetime.Scoped);
    }
}
