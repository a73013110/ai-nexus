using System.Net.Sockets;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Mail.Delivery;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace AiNexus.UnitTests.Mail;

public sealed class MailFailuresTests
{
    public static TheoryData<Exception, string, string> Cases => new()
    {
        { new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxBusy, "4.2.1 Mailbox busy"), MailAttemptOutcomes.Transient, MailFailures.Deferred },
        { new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.ServiceNotAvailable, "4.3.2 Try again later"), MailAttemptOutcomes.Transient, MailFailures.Deferred },
        { new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "5.1.1 User unknown"), MailAttemptOutcomes.Permanent, MailFailures.Rejected },
        { new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.ExceededStorageAllocation, "5.3.4 Message too big"), MailAttemptOutcomes.Permanent, MailFailures.Rejected },
        { new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "5.7.1 Invalid credentials for relay [10.0.0.5]"), MailAttemptOutcomes.Transport, MailFailures.TransportUnavailable },
        { new SmtpCommandException(SmtpErrorCode.SenderNotAccepted, SmtpStatusCode.MailboxUnavailable, "5.7.1 Sender not authorized"), MailAttemptOutcomes.Transport, MailFailures.TransportUnavailable },
        { new SmtpCommandException(SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.AuthenticationInvalidCredentials, "5.7.8 Username and Password not accepted"), MailAttemptOutcomes.Transport, MailFailures.TransportUnavailable },
        { new AuthenticationException("535"), MailAttemptOutcomes.Transport, MailFailures.TransportUnavailable },
        { new SslHandshakeException("certificate"), MailAttemptOutcomes.Transport, MailFailures.TransportUnavailable },
        { new SocketException((int)SocketError.ConnectionRefused), MailAttemptOutcomes.Transient, MailFailures.ConnectionFailed },
        { new IOException("reset"), MailAttemptOutcomes.Transient, MailFailures.ConnectionFailed },
        { new TimeoutException(), MailAttemptOutcomes.Transient, MailFailures.Timeout },
        { new InvalidOperationException("bug"), MailAttemptOutcomes.Transient, MailFailures.DeliveryFailed },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExceptionsAreClassifiedIntoSingleMessageOrTransportFailures(Exception exception, string outcome, string code)
    {
        var failure = MailFailures.Classify(exception);
        Assert.Equal(outcome, failure.Outcome);
        Assert.Equal(code, failure.ErrorCode);
        Assert.Equal(code == MailFailures.DeliveryFailed, failure.Unexpected);
    }

    [Fact]
    public void SmtpRepliesKeepTheirStatusAndEnhancedCodeButConnectionFailuresAreCounted()
    {
        var rejected = MailFailures.Classify(new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "5.1.1 <c***n@company.com> unknown"));
        Assert.Equal((550, "5.1.1", false), (rejected.SmtpStatus, rejected.EnhancedStatus, rejected.Connection));
        Assert.True(MailFailures.Classify(new SocketException()).Connection);
        Assert.True(MailFailures.Classify(new SmtpCommandException(SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.ServiceNotAvailable, "Service not available")).Connection);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 30)]
    [InlineData(5, 60)]
    [InlineData(12, 60)]
    public void BackoffGrowsToAnHourWithTwentyPercentJitter(int attempts, int minutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutes * 0.8), MailFailures.Backoff(attempts, 0));
        Assert.Equal(TimeSpan.FromMinutes(minutes), MailFailures.Backoff(attempts, 0.5));
        Assert.True(MailFailures.Backoff(attempts, 0.999) < TimeSpan.FromMinutes(minutes * 1.2));
    }
}
