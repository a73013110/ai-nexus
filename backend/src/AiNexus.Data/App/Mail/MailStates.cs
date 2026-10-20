namespace AiNexus.Data.App.Mail;

/// <summary>
/// <c>mail.OutboundMessages.Status</c> 的值（資料庫 CHECK 約束同一組）。等待重試不是獨立狀態：<see cref="Queued"/> 且 <c>NextAttemptAt</c> 在未來。
/// </summary>
public static class MailStates
{
    public const string Queued = "queued";
    public const string Sending = "sending";
    public const string Sent = "sent";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";

    public static IReadOnlyList<string> All { get; } = [Queued, Sending, Sent, Failed, Cancelled];
}
