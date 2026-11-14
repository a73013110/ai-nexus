using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Platform.Notifications;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Flow;

/// <summary>
/// 簽核的通知中心訊息與郵件：關卡啟動通知審核人、申請結束通知申請人、久未審核每天提醒一次。
/// 都只加進 ChangeTracker，與簽核變更同一個交易；郵件只寄給有已驗證信箱的人，內容不含申請詳情，請收件者登入查看。
/// </summary>
[Scoped]
public sealed class FlowNotices(AppDbContext db, FlowCatalog catalog, NotificationService notifications, MailOutbox outbox, IOptions<FlowOptions> options)
{
    /// <summary>通知的目標種類；連結由 <c>NotificationDisplay.Link</c> 推得，與 <see cref="Path"/> 相同。</summary>
    public const string TargetKind = "flow";

    /// <summary>申請在簽核頁的站內路徑。</summary>
    public static string Path(Guid instanceId) => FeatureCatalog.RouteOf(FeatureCatalog.FlowApprovals) + "?instance=" + instanceId;

    /// <summary>關卡結束：所有人關於這筆申請的未讀通知改為已讀。立即執行，呼叫端必須在交易內。</summary>
    internal Task ResolveAsync(Guid instanceId, CancellationToken ct) => notifications.ResolveAsync(TargetKind, instanceId, ct);

    internal async Task AssignedAsync(FlowCase flow, FlowInstanceStep step, IReadOnlySet<Guid> users, CancellationToken ct)
    {
        if (users.Count == 0) return;
        var instance = flow.Instance;
        var message = $"{await NameAsync(instance.ApplicantId, ct)} 送出的「{catalog.KindName(instance.Kind)}」申請等你審核（{step.Name}）。";
        await SendAsync(instance, users, $"step-{step.StepNumber}:assigned", "flow.assigned", "info", $"待審核：{instance.Title}", message, ct);
    }

    internal async Task RemindAsync(FlowCase flow, FlowInstanceStep step, IReadOnlySet<Guid> users, DateTimeOffset now, CancellationToken ct)
    {
        if (users.Count == 0 || step.ActivatedAt is not { } activatedAt) return;
        var instance = flow.Instance;
        var days = Math.Max(1, (int)(now - activatedAt).TotalDays);
        var message = $"{await NameAsync(instance.ApplicantId, ct)} 送出的「{catalog.KindName(instance.Kind)}」申請已等候 {days} 天（{step.Name}）。";
        // 每天一則：鍵含日期，同一天重跑不重送。
        await SendAsync(instance, users, $"step-{step.StepNumber}:reminded:{now.ToTaipei():yyyyMMdd}", "flow.reminder", "info", $"仍待審核：{instance.Title}", message, ct);
    }

    /// <summary>申請結束時通知申請人；取消是申請人自己的操作，不另外通知。</summary>
    internal async Task CompletedAsync(FlowCase flow, CancellationToken ct)
    {
        var instance = flow.Instance;
        var notice = instance.Status switch
        {
            FlowStatuses.Approved => ("success", $"已核准：{instance.Title}", "你的申請已核准。"),
            FlowStatuses.Rejected => ("info", $"已退回：{instance.Title}", Rejection(flow)),
            FlowStatuses.Expired => ("info", $"已逾期：{instance.Title}", $"申請超過 {options.Value.ExpireAfterDays} 天沒有完成，已自動結束；需要時請重新申請。"),
            _ => default((string Severity, string Title, string Message)?),
        };
        if (notice is not { } value) return;
        var (severity, title, message) = value;
        await SendAsync(instance, new HashSet<Guid> { instance.ApplicantId }, instance.Status, "flow.completed", severity, title, message, ct);
    }

    private static string Rejection(FlowCase flow) => flow.Steps.LastOrDefault(x => x.Status == FlowStepStatuses.Rejected) is { } step
        ? $"「{step.Name}」退回了你的申請" + (step.Comment is { } comment ? $"：{comment}" : "。")
        : "你的申請已退回。";

    private async Task SendAsync(FlowInstance instance, IReadOnlySet<Guid> users, string eventKey, string type, string severity, string title, string message, CancellationToken ct)
    {
        foreach (var user in users)
            await notifications.PublishAsync(new(user, $"flow:{instance.Id}:{eventKey}", type, severity, title, message, TargetKind, instance.Id), ct);
        if (!outbox.Available) return;
        var ids = users.ToArray();
        var recipients = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id) && x.Email != null && x.EmailVerifiedAt != null)
            .Select(x => new { x.Id, x.DisplayName, x.Email }).ToListAsync(ct);
        var mail = new FlowMail();
        foreach (var recipient in recipients)
            await outbox.EnqueueAsync(mail, new FlowMailModel(recipient.DisplayName, title, message, Path(instance.Id)),
                new MailRecipient(recipient.Email!, recipient.DisplayName, recipient.Id), new MailEnqueueOptions($"{mail.Category}:{instance.Id}:{eventKey}:{recipient.Id}"), ct);
    }

    private async Task<string> NameAsync(Guid userId, CancellationToken ct)
        => await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.DisplayName).SingleOrDefaultAsync(ct) ?? "（已刪除的使用者）";
}

/// <summary>簽核郵件的內容；<paramref name="Path"/> 是申請的站內路徑（<see cref="FlowNotices.Path"/>）。</summary>
public sealed record FlowMailModel(string DisplayName, string Subject, string Message, string Path);

/// <summary>簽核的通知信：待審核、提醒與結果共用一種版面，只有主旨與說明不同。</summary>
public sealed class FlowMail : MailTemplate<FlowMailModel>
{
    public override string Category => "flow.notice";
    public override string Subject(FlowMailModel model) => model.Subject;

    public override void Body(FlowMailModel model, MailBody body)
        => body.Heading(model.Subject).Paragraph($"{model.DisplayName} 您好，{model.Message}").Button("開啟申請", model.Path)
            .Note("申請內容請登入 AI Nexus 查看；這封信由系統自動寄出，請勿直接回覆。");
}
