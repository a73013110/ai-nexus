using System.Net.Sockets;
using System.Text.RegularExpressions;
using AiNexus.Data.App.Mail;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>
/// 一次寄送失敗的分類。<paramref name="Outcome"/> 見 <see cref="MailAttemptOutcomes"/>；<paramref name="Connection"/> 表示連線層失敗（連續 3 次開斷路器）；
/// <paramref name="Unexpected"/> 是無法分類的例外，會產生查證代碼。
/// </summary>
public sealed record MailFailure(string Outcome, string ErrorCode, int? SmtpStatus, string? EnhancedStatus, bool Connection, bool Unexpected, string ErrorType);

/// <summary>
/// 把 MailKit 的例外與 SMTP 回應分類成：單封暫時性（退避重試）、單封永久性（不再重試）、傳輸層故障（設定或服務整體壞掉，暫停寄送）。
/// 傳輸層故障不讓每封信各自耗盡重試：認證、寄件者、relay 拒絕與 TLS 失敗對每一封都一樣。
/// </summary>
public static partial class MailFailures
{
    public const string Rejected = "mail_rejected";
    public const string Expired = "mail_expired";
    public const string GaveUp = "mail_gave_up";
    public const string TransportUnavailable = "mail_transport_unavailable";
    public const string Timeout = "mail_timeout";
    public const string ConnectionFailed = "mail_connection_failed";
    public const string Deferred = "mail_deferred";
    public const string DeliveryFailed = "mail_delivery_failed";

    public static MailFailure Classify(Exception exception) => exception switch
    {
        SmtpCommandException command => Command(command),
        // MailKit 與 .NET 的 TLS／認證例外：設定錯誤或憑證不符，每一封都會失敗。
        MailKit.Security.AuthenticationException or System.Security.Authentication.AuthenticationException or SslHandshakeException or ServiceNotAuthenticatedException
            => new(MailAttemptOutcomes.Transport, TransportUnavailable, null, null, false, false, exception.GetType().Name),
        TimeoutException or OperationCanceledException => new(MailAttemptOutcomes.Transient, Timeout, null, null, true, false, exception.GetType().Name),
        SmtpProtocolException or ServiceNotConnectedException or IOException or SocketException
            => new(MailAttemptOutcomes.Transient, ConnectionFailed, null, null, true, false, exception.GetType().Name),
        _ => new(MailAttemptOutcomes.Transient, DeliveryFailed, null, null, false, true, exception.GetType().Name),
    };

    private static MailFailure Command(SmtpCommandException command)
    {
        var status = (int)command.StatusCode;
        var enhanced = Enhanced(command.Message);
        var type = command.GetType().Name;
        // 認證失敗（530、534、535、538、5.7.0、5.7.8）與寄件者被拒：設定問題，整體暫停。
        if (status is 530 or 534 or 535 or 538 || enhanced is "5.7.0" or "5.7.8" || command.ErrorCode == SmtpErrorCode.SenderNotAccepted && status >= 500)
            return new(MailAttemptOutcomes.Transport, TransportUnavailable, status, enhanced, false, false, type);
        // relay 被拒（Google：550 5.7.1 Invalid credentials for relay）是主機 IP 或帳號未被允許，不是收件者的問題。
        if (enhanced == "5.7.1" && command.Message.Contains("relay", StringComparison.OrdinalIgnoreCase))
            return new(MailAttemptOutcomes.Transport, TransportUnavailable, status, enhanced, false, false, type);
        if (status is >= 400 and < 500)
            return new(MailAttemptOutcomes.Transient, Deferred, status, enhanced, status == 421, false, type);
        if (command.ErrorCode is SmtpErrorCode.RecipientNotAccepted or SmtpErrorCode.MessageNotAccepted || status == 552)
            return new(MailAttemptOutcomes.Permanent, Rejected, status, enhanced, false, false, type);
        // 其他 5xx（500、502、503、504）是協定或伺服器能力問題，對每一封都一樣。
        return new(MailAttemptOutcomes.Transport, TransportUnavailable, status, enhanced, false, false, type);
    }

    /// <summary>
    /// 退避：1、5、15、30、60 分鐘，之後每 60 分鐘，±20% jitter（同一批暫時失敗的信不會同時再撞伺服器）。
    /// <paramref name="jitter"/> 是 [0, 1) 的亂數，測試可固定。
    /// </summary>
    public static TimeSpan Backoff(int attempts, double jitter)
    {
        int[] minutes = [1, 5, 15, 30, 60];
        var baseline = minutes[Math.Clamp(attempts, 1, minutes.Length) - 1];
        return TimeSpan.FromMinutes(baseline * (0.8 + 0.4 * jitter));
    }

    private static string? Enhanced(string message) => EnhancedPattern().Match(message) is { Success: true } match ? match.Value : null;

    [GeneratedRegex(@"^[245]\.\d{1,3}\.\d{1,3}(?=\s|$)")]
    private static partial Regex EnhancedPattern();
}
