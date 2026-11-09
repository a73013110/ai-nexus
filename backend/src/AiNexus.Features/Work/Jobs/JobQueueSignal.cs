using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Jobs;

/// <summary>
/// 同一程序內的捷徑：有工作可認領時叫醒所有 worker（各通道的工作種類不同，只叫醒一個可能叫錯）。
/// 跨程序與租約到期一律靠輪詢，沒有它也一樣正確，只是比較慢。
/// </summary>
[Singleton]
public sealed class JobQueueSignal
{
    private TaskCompletionSource next = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>下一次叫醒時完成。要在查詢佇列之前取得，查詢之後才排入的工作才不會漏接。</summary>
    public Task Next => Volatile.Read(ref next).Task;

    public void Wake() => Interlocked.Exchange(ref next, new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
}
