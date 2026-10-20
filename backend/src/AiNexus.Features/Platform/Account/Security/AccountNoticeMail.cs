using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Mail;

namespace AiNexus.Features.Platform.Account.Security;

/// <summary>
/// 帳號安全通知的內容；<paramref name="Detail"/> 只放遮罩後的值（例如新信箱）。
/// <paramref name="ByAdministrator"/>：由管理員變更，沒有登出其他工作階段，結語改為請本人確認。
/// </summary>
public sealed record AccountNoticeMailModel(string DisplayName, string Change, string? Detail, bool ByAdministrator = false);

/// <summary>
/// 信箱或本地登入變更後的通知，寄到變更前已驗證的信箱：若不是本人操作，使用者可以及早發現。不含密碼、驗證碼與完整的新地址。
/// </summary>
public sealed class AccountNoticeMail : MailTemplate<AccountNoticeMailModel>
{
    public override string Category => "account.security-notice";
    public override MailPriority Priority => MailPriority.Security;
    public override string Subject(AccountNoticeMailModel model) => "帳號安全設定已變更";

    public override void Body(AccountNoticeMailModel model, MailBody body)
    {
        body.Heading("帳號安全設定已變更").Paragraph($"{model.DisplayName} 您好，你的帳號剛剛{model.Change}。");
        if (model.Detail is { } detail) body.Paragraph(detail);
        body.Note(model.ByAdministrator
            ? "此變更由系統管理員執行。如果你沒有提出這項需求，請立即聯絡系統管理員。"
            : "如果這不是你本人的操作，請立即聯絡系統管理員；此變更已登出你在其他裝置的工作階段。");
    }
}
