using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Mail;

namespace AiNexus.Features.Platform.Account.Security;

/// <summary>驗證碼信的內容；<paramref name="Code"/> 只出現在加密保存、寄出後即刪除的內文。</summary>
public sealed record VerificationCodeMailModel(string DisplayName, string Code, int Minutes);

/// <summary>
/// 驗證碼信：敏感（內文加密、寄出後刪除、不可重寄）且優先寄出。刻意不放可點的連結：郵件安全掃描器會預先開啟連結，
/// 跨裝置也不必處理；主旨不含驗證碼。
/// </summary>
public abstract class VerificationCodeMail : MailTemplate<VerificationCodeMailModel>
{
    public override MailPriority Priority => MailPriority.Security;
    public override bool Sensitive => true;
    protected abstract string Purpose { get; }

    public override void Body(VerificationCodeMailModel model, MailBody body) => body
        .Heading("驗證碼")
        .Paragraph($"{model.DisplayName} 您好，你正在{Purpose}，請在設定頁輸入以下驗證碼：")
        .Code(model.Code)
        .Paragraph($"驗證碼 {model.Minutes} 分鐘內有效，只能使用一次。")
        .Note("如果這不是你本人的操作，請忽略這封信，並通知系統管理員。");
}

/// <summary>寄到新信箱，證明使用者能收到這個地址的信。</summary>
public sealed class EmailChangeCodeMail : VerificationCodeMail
{
    public override string Category => "account.email-change-code";
    protected override string Purpose => "設定通知信箱";
    public override string Subject(VerificationCodeMailModel model) => "確認你的通知信箱";
}

/// <summary>寄到已驗證的信箱，供變更帳號安全設定前的步升驗證。</summary>
public sealed class ReauthenticationCodeMail : VerificationCodeMail
{
    public override string Category => "account.reauthentication-code";
    protected override string Purpose => "變更帳號安全設定";
    public override string Subject(VerificationCodeMailModel model) => "帳號安全驗證碼";
}
