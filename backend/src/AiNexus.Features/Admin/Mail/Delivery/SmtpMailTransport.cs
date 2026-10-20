using AiNexus.Data.App.Mail;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>
/// MailKit SMTP：公司 mail server 或 Google Workspace SMTP relay（<c>smtp-relay.gmail.com:587</c>）、Gmail SMTP（<c>smtp.gmail.com:587</c>＋App Password）。
/// 憑證完整驗證，只容忍「撤銷狀態查不到」；自簽或不可信的內部 server 以指紋釘選，不提供「略過驗證」（規則在 <see cref="SmtpCertificate"/>）。
/// 失敗包成 <see cref="MailSendException"/>，帶出失敗的步驟與憑證摘要。
/// </summary>
public sealed class SmtpMailTransport(IOptions<MailOptions> options) : IMailTransport
{
    public string Name => MailTransports.Smtp;

    public Task<IMailSession> OpenAsync(CancellationToken ct) => Task.FromResult<IMailSession>(new Session(options.Value.Smtp));

    private sealed class Session : IMailSession
    {
        private readonly MailSmtpOptions smtp;
        private readonly SmtpClient client;
        private SmtpCertificate? certificate;

        public Session(MailSmtpOptions smtp)
        {
            this.smtp = smtp;
            client = SmtpConnector.Configure(new SmtpClient(), smtp, observed => certificate = observed);
        }

        public async Task<string> SendAsync(OutgoingMail mail, CancellationToken ct)
        {
            if (!client.IsConnected) await ConnectAsync(ct);
            try
            {
                return await client.SendAsync(mail.Message, ct);
            }
            catch (SmtpCommandException ex)
            {
                // 單封被拒時連線仍可用，不斷開。
                throw new MailSendException(ex.ErrorCode switch
                {
                    SmtpErrorCode.SenderNotAccepted => MailAttemptStages.Sender,
                    SmtpErrorCode.RecipientNotAccepted => MailAttemptStages.Recipient,
                    _ => MailAttemptStages.Data,
                }, null, ex);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // 協定或連線層的失敗會讓連線處於未知狀態；斷開後下一封重新連線。
                await DisconnectQuietlyAsync();
                throw new MailSendException(MailAttemptStages.Data, null, ex);
            }
        }

        private async Task ConnectAsync(CancellationToken ct)
        {
            certificate = null;
            try
            {
                await client.ConnectAsync(smtp.Host, smtp.Port, SmtpConnector.Security(smtp.Security), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // 驗證回呼有被叫到就代表已進入 TLS 交握；之前的失敗是 TCP、問候或 EHLO。
                var tls = ex is SslHandshakeException || certificate is not null;
                await DisconnectQuietlyAsync();
                throw new MailSendException(tls ? MailAttemptStages.Tls : MailAttemptStages.Connect, certificate, ex);
            }
            if (smtp.UserName.Length == 0) return;
            try
            {
                await client.AuthenticateAsync(smtp.UserName, smtp.Password, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                await DisconnectQuietlyAsync();
                throw new MailSendException(MailAttemptStages.Auth, null, ex);
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
    }
}
