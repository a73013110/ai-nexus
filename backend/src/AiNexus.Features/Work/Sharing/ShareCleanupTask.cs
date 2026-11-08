using AiNexus.Platform.Scheduling;

namespace AiNexus.Features.Work.Sharing;

/// <summary>過期或撤銷的分享不再授權檔案，過期分享的快照文字清空；單一短交易。</summary>
internal sealed class ShareCleanupTask(ShareService shares) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "sharing.cleanup", "分享清理", "分享", "移除過期或已撤銷分享的檔案授權，並清空過期分享的快照。", Schedule.Every(TimeSpan.FromMinutes(20)));

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
        => TaskOutcome.Count(await shares.PurgeExpiredAsync(ct), total => $"清理 {total:N0} 筆");
}
