using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Admin.Mail.Delivery;
using AiNexus.Features.Admin.Mail.Transport;
using AiNexus.UnitTests.Support;

namespace AiNexus.UnitTests.Mail;

public sealed class SmtpProbeTests
{
    private const string From = "hlmail@company.test", To = "chen@company.test";

    [Fact]
    public async Task APlainLoopbackServerPassesEveryStepWithoutSendingAndMasksTheCredentials()
    {
        await using var server = new FakeSmtpServer();
        var result = await RunAsync(Smtp(server.Port, MailSmtpOptions.NoSecurity, "relay", "s3cret!"));

        Assert.Equal(
            [("connect", "passed"), ("ehlo", "passed"), ("tls", "skipped"), ("certificate", "skipped"), ("auth", "passed"), ("sender", "passed"), ("recipient", "passed"), ("reset", "passed")],
            result.Steps.Select(x => (x.Id, x.Status)));
        Assert.Equal(250, result.Steps.Single(x => x.Id == MailDiagnosisSteps.Recipient).SmtpStatus);
        Assert.Contains("AUTH LOGIN PLAIN", result.Capabilities);
        Assert.Contains($"C: RCPT TO:<{To}>", result.Transcript, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(Encoding.ASCII.GetBytes("s3cret!")), result.Transcript, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(Encoding.ASCII.GetBytes("relay")), result.Transcript, StringComparison.Ordinal);
        Assert.Empty(server.Messages);
        Assert.DoesNotContain(server.Commands, x => x.StartsWith("DATA", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARejectedRecipientStopsTheProbeAndLaterStepsAreSkipped()
    {
        await using var server = new FakeSmtpServer { RecipientReply = _ => "550 5.1.1 User unknown" };
        var result = await RunAsync(Smtp(server.Port, MailSmtpOptions.NoSecurity));

        var recipient = result.Steps.Single(x => x.Id == MailDiagnosisSteps.Recipient);
        Assert.Equal(("failed", 550), (recipient.Status, recipient.SmtpStatus));
        Assert.Contains("User unknown", recipient.Summary, StringComparison.Ordinal);
        Assert.Equal("skipped", result.Steps.Single(x => x.Id == MailDiagnosisSteps.Reset).Status);
        Assert.Equal("skipped", result.Steps.Single(x => x.Id == MailDiagnosisSteps.Auth).Status);
    }

    [Fact]
    public async Task RejectedCredentialsFailAtAuthentication()
    {
        await using var server = new FakeSmtpServer { AcceptAuth = false };
        var result = await RunAsync(Smtp(server.Port, MailSmtpOptions.NoSecurity, "relay", "wrong"));
        var auth = result.Steps.Single(x => x.Id == MailDiagnosisSteps.Auth);
        Assert.Equal("failed", auth.Status);
        Assert.Contains("LOGIN", auth.Summary, StringComparison.Ordinal);
        Assert.Equal("skipped", result.Steps.Single(x => x.Id == MailDiagnosisSteps.Sender).Status);
    }

    [Fact]
    public async Task AClosedPortFailsAtTheTcpStep()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var result = await RunAsync(Smtp(port, MailSmtpOptions.NoSecurity));
        Assert.Equal(("connect", "failed"), (result.Steps[0].Id, result.Steps[0].Status));
        Assert.All(result.Steps.Skip(1), x => Assert.Equal("skipped", x.Status));
        Assert.Null(result.Certificate);
    }

    [Fact]
    public async Task AnUntrustedCertificateFailsAtTlsAndShowsTheThumbprintToPin()
    {
        using var certificate = FakeSmtpServer.SelfSigned();
        await using var server = new FakeSmtpServer(certificate);
        var result = await RunAsync(Smtp(server.Port, MailSmtpOptions.StartTls, "relay", "s3cret!"));

        Assert.Equal([("connect", "passed"), ("ehlo", "passed"), ("tls", "failed"), ("certificate", "failed")], result.Steps.Take(4).Select(x => (x.Id, x.Status)));
        Assert.Equal("skipped", result.Steps.Single(x => x.Id == MailDiagnosisSteps.Auth).Status);
        Assert.Equal((SmtpCertificateVerdicts.Rejected, Convert.ToHexString(certificate.GetCertHash(HashAlgorithmName.SHA256))), (result.Certificate!.Verdict, result.Certificate.Sha256));
        Assert.Contains("UntrustedRoot", result.Certificate.ChainStatus);
    }

    [Fact]
    public async Task APinnedCertificatePassesWithAWarningAndAuthenticatesOverTls()
    {
        using var certificate = FakeSmtpServer.SelfSigned();
        await using var server = new FakeSmtpServer(certificate);
        var smtp = Smtp(server.Port, MailSmtpOptions.StartTls, "relay", "s3cret!");
        smtp.ServerCertificateThumbprint = Convert.ToHexString(certificate.GetCertHash(HashAlgorithmName.SHA256));
        var result = await RunAsync(smtp);

        Assert.Equal(
            [("connect", "passed"), ("ehlo", "passed"), ("tls", "passed"), ("certificate", "warning"), ("auth", "passed"), ("sender", "passed"), ("recipient", "passed"), ("reset", "passed")],
            result.Steps.Select(x => (x.Id, x.Status)));
        Assert.Equal(SmtpCertificateVerdicts.Pinned, result.Certificate!.Verdict);
        Assert.Contains("C: STARTTLS", result.Transcript, StringComparison.Ordinal);
    }

    internal static MailSmtpOptions Smtp(int port, string security, string user = "", string password = "")
        => new() { Host = "127.0.0.1", Port = port, Security = security, UserName = user, Password = password, TimeoutSeconds = 5 };

    private static async Task<MailDiagnosisResult> RunAsync(MailSmtpOptions smtp)
    {
        using var probe = new SmtpProbe(smtp, TimeProvider.System);
        return await probe.RunAsync(From, To, TestContext.Current.CancellationToken);
    }
}
