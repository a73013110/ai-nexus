using AiNexus.Platform.Scheduling;

namespace AiNexus.Features.Work.Files.Storage;

/// <summary>回收過期草稿、補刪待刪的檔案，並對帳刪除資料庫已不認得的孤兒檔。</summary>
internal sealed class AttachmentCleanupTask(AttachmentLifecycle lifecycle) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "files.attachment-cleanup", "附件清理", "檔案", "回收過期的上傳草稿、補刪待刪的檔案，並刪除儲存區裡沒有紀錄的孤兒檔。");

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        context.Report("回收過期草稿");
        var drafts = await lifecycle.ReclaimAsync(ct);
        context.Report("對帳儲存區");
        var orphans = await lifecycle.ReconcileAsync(ct);
        return drafts + orphans == 0 ? TaskOutcome.Idle() : TaskOutcome.Done($"草稿 {drafts:N0}、孤兒檔 {orphans:N0}");
    }
}
