using AiNexus.Data.App.Jobs;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Jobs;

/// <summary>
/// 執行一種工作。預期內的失敗以回傳值表示，並記為工作的錯誤代碼；例外（外部服務、資料庫等）同樣會讓工作失敗，
/// 若為 <see cref="ExternalServiceException"/> 則沿用其代碼。
/// </summary>
public interface IBackgroundJobHandler
{
    string Kind { get; }
    Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct);

    /// <summary>判斷失敗或已取消的工作能否再執行；這裡回傳的錯誤即為重試請求的回應。</summary>
    Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct);
}
