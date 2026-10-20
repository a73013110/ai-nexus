using System.Security.Cryptography;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Admin.Mail.Delivery;
using AiNexus.UnitTests.Support;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AiNexus.UnitTests.Mail;

public sealed class SmtpMailTransportTests
{
    [Fact]
    public async Task APinnedServerReceivesTheMessageOverTls()
    {
        using var certificate = FakeSmtpServer.SelfSigned();
        await using var server = new FakeSmtpServer(certificate);
        var smtp = SmtpProbeTests.Smtp(server.Port, MailSmtpOptions.StartTls, "relay", "s3cret!");
        smtp.ServerCertificateThumbprint = Convert.ToHexString(certificate.GetCertHash(HashAlgorithmName.SHA256));

        var response = await SendAsync(smtp);
        Assert.StartsWith("2.6.0 Queued", response, StringComparison.Ordinal);
        Assert.Contains("Subject: hello", Assert.Single(server.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUntrustedCertificateFailsAtTheTlsStageWithTheCertificateSummary()
    {
        using var certificate = FakeSmtpServer.SelfSigned();
        await using var server = new FakeSmtpServer(certificate);
        var error = await Assert.ThrowsAsync<MailSendException>(() => SendAsync(SmtpProbeTests.Smtp(server.Port, MailSmtpOptions.StartTls)));
        Assert.Equal((MailAttemptStages.Tls, SmtpCertificateVerdicts.Rejected), (error.Stage, error.Certificate?.Verdict));

        var failure = MailFailures.Classify(error);
        Assert.Equal((MailAttemptOutcomes.Transport, MailFailures.TransportUnavailable, MailAttemptStages.Tls), (failure.Outcome, failure.ErrorCode, failure.Stage));
        Assert.StartsWith("verdict=rejected; policy=RemoteCertificateChainErrors", failure.Detail, StringComparison.Ordinal);
        Assert.Contains(Convert.ToHexString(certificate.GetCertHash(HashAlgorithmName.SHA256)), failure.Detail, StringComparison.Ordinal);
        Assert.Contains("UntrustedRoot", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectedCredentialsAndRecipientsAreAttributedToTheirStage()
    {
        await using var server = new FakeSmtpServer { AcceptAuth = false };
        var auth = await Assert.ThrowsAsync<MailSendException>(() => SendAsync(SmtpProbeTests.Smtp(server.Port, MailSmtpOptions.NoSecurity, "relay", "wrong")));
        Assert.Equal((MailAttemptStages.Auth, MailFailures.TransportUnavailable), (auth.Stage, MailFailures.Classify(auth).ErrorCode));

        server.AcceptAuth = true;
        server.RecipientReply = _ => "550 5.1.1 <chen@company.test>: Recipient address rejected";
        var recipient = await Assert.ThrowsAsync<MailSendException>(() => SendAsync(SmtpProbeTests.Smtp(server.Port, MailSmtpOptions.NoSecurity, "relay", "s3cret!")));
        var failure = MailFailures.Classify(recipient);
        Assert.Equal((MailAttemptStages.Recipient, MailAttemptOutcomes.Permanent, 550, "550 5.1.1"), (failure.Stage, failure.Outcome, failure.SmtpStatus, failure.Reason));
        // 寄送嘗試保存的細節會遮罩 email。
        Assert.DoesNotContain("chen@company.test", failure.Detail, StringComparison.Ordinal);
        Assert.Contains("Recipient address rejected", failure.Detail, StringComparison.Ordinal);
    }

    private static async Task<string> SendAsync(MailSmtpOptions smtp)
    {
        var transport = new SmtpMailTransport(Options.Create(new MailOptions { Transport = MailTransports.Smtp, Smtp = smtp }));
        await using var session = await transport.OpenAsync(TestContext.Current.CancellationToken);
        var message = new MimeMessage { Subject = "hello", Body = new TextPart("plain") { Text = "hi" } };
        message.From.Add(MailboxAddress.Parse("hlmail@company.test"));
        message.To.Add(MailboxAddress.Parse("chen@company.test"));
        return await session.SendAsync(new(Guid.NewGuid(), message), TestContext.Current.CancellationToken);
    }
}
