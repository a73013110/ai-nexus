using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Features.Admin.Mail;
using AiNexus.Platform.Events;

namespace AiNexus.Features.Platform.Account.Security;

/// <summary>
/// 管理員變更通知信箱後，寄通知到變更前已驗證的信箱（與自助變更同一範本）：本人能及早發現非預期的變更。
/// 在管理交易內入列，變更回滾時信件也不會寄出；沒有已驗證的舊信箱或郵件停用時略過。
/// </summary>
internal sealed class NotifyAdministeredEmailChange(MailOutbox outbox) : IDomainEventHandler<UserEmailChanged>
{
    public async Task HandleAsync(UserEmailChanged e, CancellationToken ct)
    {
        if (e.PreviousVerifiedEmail is not { } previous || !outbox.Available || UserEmails.Same(previous, e.Email)) return;
        var (change, detail) = e.Email is null
            ? ("的通知信箱已由系統管理員清除", (string?)null)
            : ("的通知信箱已由系統管理員變更", $"新的通知信箱：{UserEmails.Mask(e.Email)}（尚待你本人驗證）");
        await outbox.EnqueueAsync(new AccountNoticeMail(), new AccountNoticeMailModel(e.DisplayName, change, detail, ByAdministrator: true),
            new MailRecipient(previous, e.DisplayName, e.UserId), new MailEnqueueOptions(RequestedBy: e.ActorId), ct);
    }
}
