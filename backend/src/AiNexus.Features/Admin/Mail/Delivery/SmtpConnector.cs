using MailKit.Net.Smtp;
using MailKit.Security;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>寄送與連線診斷共用的 SMTP 連線設定，確保診斷看到的就是寄送時的行為（逾時、EHLO 網域、憑證規則）。</summary>
internal static class SmtpConnector
{
    /// <summary>套用設定；<paramref name="observe"/> 收到每次 TLS 驗證的憑證摘要（包含被拒的），失敗時才能說明原因。</summary>
    public static TClient Configure<TClient>(TClient client, MailSmtpOptions smtp, Action<SmtpCertificate> observe) where TClient : SmtpClient
    {
        client.Timeout = smtp.TimeoutSeconds * 1000;
        if (smtp.LocalDomain.Length > 0) client.LocalDomain = smtp.LocalDomain;
        // 撤銷檢查保持開啟：沒有它，鏈狀態就不會出現 Revoked。查不到撤銷狀態時由 SmtpCertificate 判定 soft-fail。
        client.CheckCertificateRevocation = true;
        client.ServerCertificateValidationCallback = (_, certificate, chain, errors) =>
        {
            var summary = SmtpCertificate.Evaluate(certificate, chain, errors, smtp.ServerCertificateThumbprint);
            observe(summary);
            return summary.Accepted;
        };
        return client;
    }

    public static SecureSocketOptions Security(string value) => value switch
    {
        MailSmtpOptions.SslOnConnect => SecureSocketOptions.SslOnConnect,
        MailSmtpOptions.NoSecurity => SecureSocketOptions.None,
        _ => SecureSocketOptions.StartTls,
    };
}
