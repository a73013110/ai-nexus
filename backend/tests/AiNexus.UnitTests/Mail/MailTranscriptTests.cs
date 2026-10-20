using System.Text;
using AiNexus.Features.Admin.Mail.Delivery;

namespace AiNexus.UnitTests.Mail;

public sealed class MailTranscriptTests
{
    [Fact]
    public void AuthenticationExchangesAreMaskedUntilTheServerAnswers()
    {
        var user = Convert.ToBase64String(Encoding.ASCII.GetBytes("hlmail"));
        var password = Convert.ToBase64String(Encoding.ASCII.GetBytes("s3cret!"));
        using var transcript = new MailTranscript();
        Client(transcript, "EHLO host\r\n");
        Server(transcript, "250-fake.test\r\n250-STARTTLS\r\n250 AUTH LOGIN\r\n");
        Client(transcript, "STARTTLS\r\n");
        Server(transcript, "220 Ready\r\n");
        Client(transcript, "AUTH LOGIN\r\n");
        Server(transcript, "334 VXNlcm5hbWU6\r\n");
        Client(transcript, user + "\r\n");
        Server(transcript, "334 UGFzc3dvcmQ6\r\n");
        Client(transcript, password + "\r\n");
        Server(transcript, "235 2.7.0 Authentication successful\r\n");
        Client(transcript, "MAIL FROM:<hlmail@company.test>\r\n");

        var text = transcript.ToString();
        Assert.DoesNotContain(user, text, StringComparison.Ordinal);
        Assert.DoesNotContain(password, text, StringComparison.Ordinal);
        Assert.Contains("C: AUTH LOGIN\n", text, StringComparison.Ordinal);
        Assert.Contains("S: 250-STARTTLS\n", text, StringComparison.Ordinal);
        // 驗證結束後的指令照常記錄。
        Assert.EndsWith("C: MAIL FROM:<hlmail@company.test>\n", text, StringComparison.Ordinal);
        Assert.Equal(("LOGIN", true), (transcript.Mechanism, transcript.StartTlsSent));
    }

    [Fact]
    public void AnInitialResponseOnTheAuthLineIsMasked()
    {
        var initial = Convert.ToBase64String(Encoding.ASCII.GetBytes("\0hlmail\0s3cret!"));
        using var transcript = new MailTranscript();
        Client(transcript, $"AUTH PLAIN {initial}\r\n");
        Server(transcript, "535 5.7.3 Authentication unsuccessful\r\n");
        Assert.Equal("C: AUTH PLAIN ********\nS: 535 5.7.3 Authentication unsuccessful\n", transcript.ToString());
        Assert.False(transcript.StartTlsSent);
    }

    private static void Client(MailTranscript transcript, string text) { var bytes = Encoding.ASCII.GetBytes(text); transcript.LogClient(bytes, 0, bytes.Length); }

    private static void Server(MailTranscript transcript, string text) { var bytes = Encoding.ASCII.GetBytes(text); transcript.LogServer(bytes, 0, bytes.Length); }
}
