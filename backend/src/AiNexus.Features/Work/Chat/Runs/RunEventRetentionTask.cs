using AiNexus.Data.App;
using AiNexus.Platform.Data;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Chat.Runs;

/// <summary>
/// 生成事件的保存期清理。只刪 SSE 斷線重播用的中繼事件（RunEvents），不動對話與訊息。
/// 分批刪除（<see cref="Batches"/>）。
/// </summary>
public sealed class RunEventRetentionTask(AppDbContext db, TimeProvider clock) : IScheduledTask
{
    /// <summary>已結束的生成，事件保存 24 小時。</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public static ScheduledTaskDefinition Definition { get; } = new(
        "chat.run-event-retention", "生成事件清理", "聊天", "刪除已結束超過 24 小時的生成重播事件；對話與訊息不受影響。", Schedule.Every(TimeSpan.FromHours(1)));

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
        => TaskOutcome.Count(await CleanupAsync(ct), total => $"刪除 {total:N0} 筆事件");

    /// <summary>分批刪除已過期且所屬生成已結束的事件，回傳刪除的總筆數。</summary>
    public async Task<int> CleanupAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow() - Retention;
        var expired = db.RunEvents.Where(x => x.CreatedAt < cutoff && db.GenerationRuns.Any(r => r.Id == x.RunId && r.ActiveOwnerId == null));
        return await Batches.RepeatAsync(() => expired.Take(Batches.Size).ExecuteDeleteAsync(ct));
    }
}
