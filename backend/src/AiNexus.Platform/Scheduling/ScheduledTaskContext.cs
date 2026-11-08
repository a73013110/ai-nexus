using System.Text.Json;

namespace AiNexus.Platform.Scheduling;

public enum ScheduledTaskTrigger
{
    Scheduled,
    Manual,
    Startup,
}

/// <summary>任務回報的進度；<see cref="Total"/> 未知時為 null，前端顯示不定進度。</summary>
public sealed record ScheduledTaskProgress(string? Stage, int? Completed, int? Total);

/// <summary>
/// 一輪執行的資訊與進度回報。<see cref="Report"/> 只更新記憶體，引擎在續租時一併寫入資料庫（數秒一次），
/// 所以迴圈裡每處理一筆就呼叫也不會多打資料庫。
/// </summary>
public sealed class ScheduledTaskContext(ScheduledTaskTrigger trigger, Guid? requestedBy, string? parameters)
{
    private ScheduledTaskProgress? progress;

    public ScheduledTaskTrigger Trigger => trigger;

    /// <summary>手動執行的管理員；排程、啟動或系統要求時為 null。</summary>
    public Guid? RequestedBy => requestedBy;

    public ScheduledTaskProgress? Progress => Volatile.Read(ref progress);

    /// <summary>手動執行附帶的參數；沒有參數時為 <c>default</c>。參數由提出要求的模組序列化，型別要與它一致。</summary>
    public T? Parameters<T>() => parameters is null ? default : JsonSerializer.Deserialize<T>(parameters, JsonSerializerOptions.Web);

    public void Report(string? stage, int? completed = null, int? total = null) =>
        Volatile.Write(ref progress, new ScheduledTaskProgress(stage, completed, total));
}
