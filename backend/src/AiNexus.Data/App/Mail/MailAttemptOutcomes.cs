namespace AiNexus.Data.App.Mail;

/// <summary><c>mail.DeliveryAttempts.Outcome</c> 的值（資料庫 CHECK 約束同一組）。</summary>
public static class MailAttemptOutcomes
{
    public const string Sent = "sent";
    /// <summary>單封暫時性失敗，依退避時間再寄。</summary>
    public const string Transient = "transient";
    /// <summary>單封永久性失敗，不再重試。</summary>
    public const string Permanent = "permanent";
    /// <summary>傳輸層故障（認證、TLS、relay 拒絕），整體暫停寄送。</summary>
    public const string Transport = "transport";
    /// <summary>租約過期仍未完成（程序中止），由下一次認領補記。</summary>
    public const string Abandoned = "abandoned";
}
