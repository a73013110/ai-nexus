using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>
/// MailKit SMTP：公司 mail server 或 Google Workspace SMTP relay（<c>smtp-relay.gmail.com:587</c>）、Gmail SMTP（<c>smtp.gmail.com:587</c>＋App Password）。
/// 憑證預設完整驗證；自簽的內部 server 以指紋釘選，不提供「略過驗證」。
/// </summary>
public sealed class SmtpMailTransport(IOptions<MailOptions> options) : IMailTransport
{
    public string Name => MailTransports.Smtp;

    public Task<IMailSession> OpenAsync(CancellationToken ct) => Task.FromResult<IMailSession>(new Session(options.Value.Smtp));

    private sealed class Session(MailSmtpOptions smtp) : IMailSession
    {
        private readonly SmtpClient client = new();

        public async Task<string> SendAsync(OutgoingMail mail, CancellationToken ct)
        {
            if (!client.IsConnected)
            {
                client.Timeout = smtp.TimeoutSeconds * 1000;
                if (smtp.LocalDomain.Length > 0) client.LocalDomain = smtp.LocalDomain;
                if (smtp.ServerCertificateThumbprint.Length > 0) client.ServerCertificateValidationCallback = (_, certificate, _, errors) => Pinned(certificate, errors, smtp.ServerCertificateThumbprint);
                await client.ConnectAsync(smtp.Host, smtp.Port, Security(smtp.Security), ct);
                if (smtp.UserName.Length > 0) await client.AuthenticateAsync(smtp.UserName, smtp.Password, ct);
            }
            try
            {
                return await client.SendAsync(mail.Message, ct);
            }
            catch (Exception ex) when (ex is not SmtpCommandException)
            {
                // 協定或連線層的失敗會讓連線處於未知狀態；斷開後下一封重新連線。單封被拒（SmtpCommandException）時連線仍可用。
                await DisconnectQuietlyAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (client.IsConnected)
            {
                try { await client.DisconnectAsync(quit: true, CancellationToken.None); }
                catch (Exception ex) when (ex is IOException or SmtpProtocolException or ServiceNotConnectedException or OperationCanceledException) { }
            }
            client.Dispose();
        }

        private async Task DisconnectQuietlyAsync()
        {
            try { if (client.IsConnected) await client.DisconnectAsync(quit: false, CancellationToken.None); }
            catch (Exception ex) when (ex is IOException or SmtpProtocolException or ServiceNotConnectedException or OperationCanceledException) { }
        }

        private static SecureSocketOptions Security(string value) => value switch
        {
            MailSmtpOptions.SslOnConnect => SecureSocketOptions.SslOnConnect,
            MailSmtpOptions.NoSecurity => SecureSocketOptions.None,
            _ => SecureSocketOptions.StartTls,
        };

        // 只在指紋相符時接受；名稱不符或鏈不完整的自簽憑證因此可用，但換了憑證就會失敗（傳輸層故障，暫停寄送）。
        private static bool Pinned(X509Certificate? certificate, SslPolicyErrors errors, string thumbprint)
        {
            if (errors == SslPolicyErrors.None) return true;
            if (certificate is null) return false;
            var expected = Convert.FromHexString(thumbprint);
            var actual = expected.Length == 20 ? certificate.GetCertHash() : certificate.GetCertHash(HashAlgorithmName.SHA256);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}
