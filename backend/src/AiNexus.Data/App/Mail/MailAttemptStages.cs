namespace AiNexus.Data.App.Mail;

/// <summary><c>mail.DeliveryAttempts.Stage</c> 的值：失敗發生在 SMTP 對話的哪一步（資料庫 CHECK 約束同一組）。</summary>
public static class MailAttemptStages
{
    /// <summary>TCP 連線、伺服器問候或 EHLO。</summary>
    public const string Connect = "connect";
    /// <summary>STARTTLS 或 TLS 交握（含憑證驗證）。</summary>
    public const string Tls = "tls";
    public const string Auth = "auth";
    /// <summary>MAIL FROM。</summary>
    public const string Sender = "sender";
    /// <summary>RCPT TO。</summary>
    public const string Recipient = "recipient";
    /// <summary>DATA 與內容傳送；寄送中途斷線也歸在這裡。</summary>
    public const string Data = "data";
}
