using System.Security.Cryptography;
using System.Text;
using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Notifications;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Scheduling.Engine;

/// <summary>
/// 任務連續失敗達到門檻時通知能看背景作業頁的人；恢復正常時撤下。只在跨過門檻的那一輪寫，不會每輪都發。
/// </summary>
[Scoped]
public sealed class FailureAlerts(AppDbContext db, AccessService access, NotificationService notifications)
{
    public const string TargetKind = "scheduled-task";

    /// <summary>通知的目標是任務 Key；目標欄位是 GUID，所以由 Key 雜湊出固定值，同一任務的提醒才能互相取代與撤下。</summary>
    public static Guid TargetOf(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));

    public async Task RaiseAsync(ScheduledTaskPlan plan, Guid runId, int failures, string? issueCode, CancellationToken ct)
    {
        var holders = await access.HoldersAsync(FeatureCatalog.Scheduling, ct);
        var body = $"已連續失敗 {failures} 次，請到背景作業頁查看錯誤並處理。";
        // 通知的 Key 每一輪不同：恢復後再次失敗要能再通知，同一輪重送才去重。
        foreach (var holder in holders)
        {
            await notifications.PublishAsync(
                // 不用 error 等級：error 會換成通用的「操作未完成」，管理員就看不出是哪個任務。
                new NotificationMessage(holder, $"scheduled-task:{runId:N}", "scheduling.task_failing", "info",
                    $"排程任務「{plan.Definition.Name}」失敗", body, TargetKind, TargetOf(plan.Key))
                { IssueCode = issueCode }, ct);
        }
        await db.SaveChangesAsync(ct);
    }

    public Task ResolveAsync(string key, CancellationToken ct) => notifications.ResolveAsync(TargetKind, TargetOf(key), ct);
}
