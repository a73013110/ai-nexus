using AiNexus.Platform.Modules;

namespace AiNexus.Platform.Scheduling;

/// <summary>
/// 一個排程任務：依 <see cref="Definition"/> 的排程定期執行，可在「背景作業」頁看到下次執行時間、執行中狀態與紀錄，也能手動執行、停止或暫停。
/// 實作只寫業務邏輯，迴圈、跨程序租約、逾時、取消、日誌與紀錄由排程引擎處理；每輪一個 DI 範圍（scoped）。
/// </summary>
/// <remarks>
/// 引擎保證同一任務同時只有一個程序在跑，但執行可能在任何時刻被中斷（停站、停止）後重跑，所以實作必須可重複執行：
/// 分批提交、以資料狀態判斷還要做什麼，不依賴上一輪留在記憶體的東西。預期內的失敗回傳 <see cref="TaskOutcome.Failed"/>，
/// 例外同樣記為失敗（<c>ExternalServiceException</c> 沿用其錯誤代碼）。
/// </remarks>
[RegisterImplementations(typeof(ScheduledTaskRegistrations), nameof(ScheduledTaskRegistrations.AddScheduledTask))]
public interface IScheduledTask
{
    /// <summary>任務的識別、名稱與排程；Key 寫進資料庫，改名等於新任務。</summary>
    static abstract ScheduledTaskDefinition Definition { get; }

    Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct);
}

/// <summary>已註冊的任務：定義與在指定範圍內執行它的方法。介面有 static abstract 成員，不能當型別引數，所以以委派轉接。</summary>
public sealed record ScheduledTaskRegistration(
    ScheduledTaskDefinition Definition,
    Func<IServiceProvider, ScheduledTaskContext, CancellationToken, Task<TaskOutcome>> Run);

public static class ScheduledTaskRegistrations
{
    /// <summary>由組合產生器呼叫；Key 重複時排程目錄在啟動時失敗。</summary>
    public static IServiceCollection AddScheduledTask<TTask>(this IServiceCollection services) where TTask : class, IScheduledTask
    {
        services.AddScoped<TTask>();
        return services.AddSingleton(new ScheduledTaskRegistration(
            TTask.Definition,
            static (provider, context, ct) => provider.GetRequiredService<TTask>().RunAsync(context, ct)));
    }
}
