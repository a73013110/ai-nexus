using System.Globalization;

namespace AiNexus.Features.Admin.Mail.Messages;

/// <summary>測試信的內容：誰在何時、經哪個傳輸要求的。</summary>
public sealed record TestMailModel(string RequestedBy, DateTimeOffset RequestedAt, string Transport, string From);

/// <summary>管理頁「寄測試信」的範本：確認設定能寄出，內容不含任何秘密。</summary>
public sealed class TestMail : MailTemplate<TestMailModel>
{
    public override string Category => "mail.test";

    public override string Subject(TestMailModel model) => "AI Nexus 郵件設定測試";

    public override void Body(TestMailModel model, MailBody body) => body
        .Heading("郵件設定測試")
        .Paragraph("收到這封信，代表 AI Nexus 的郵件設定可以寄出信件。")
        .KeyValue("要求者", model.RequestedBy)
        .KeyValue("時間（UTC）", model.RequestedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
        .KeyValue("傳輸", model.Transport)
        .KeyValue("寄件地址", model.From)
        .Note("如果你沒有預期收到這封信，可以直接刪除。");
}
