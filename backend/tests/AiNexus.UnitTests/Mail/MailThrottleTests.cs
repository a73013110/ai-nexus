using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Admin.Mail.Delivery;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.UnitTests.Mail;

public sealed class MailThrottleTests
{
    [Fact]
    public void TheBucketHoldsOneMinuteOfSendsAndRefillsContinuously()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var throttle = new MailThrottle(Options.Create(new MailOptions { MaxPerMinute = 60 }), clock);
        Assert.Equal(20, throttle.Take(20));
        Assert.Equal(40, throttle.Take(100));
        Assert.Equal(0, throttle.Take(1));
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(10, throttle.Take(100));
        throttle.Return(3);
        Assert.Equal(3, throttle.Take(100));
        clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(60, throttle.Take(1000));
    }

    [Fact]
    public void TheCircuitOpensOnceForTransportFailuresOrRepeatedConnectionFailures()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var circuit = new MailCircuit(clock);
        Assert.False(circuit.ConnectionFailed(MailFailures.ConnectionFailed));
        Assert.False(circuit.ConnectionFailed(MailFailures.ConnectionFailed));
        Assert.True(circuit.ConnectionFailed(MailFailures.ConnectionFailed));
        Assert.True(circuit.IsOpen);
        Assert.False(circuit.Open(MailFailures.TransportUnavailable));
        clock.Advance(MailCircuit.OpenFor);
        Assert.False(circuit.IsOpen);
        Assert.True(circuit.Succeeded());
        Assert.False(circuit.Succeeded());
        Assert.Null(circuit.OpenUntil);
        Assert.True(circuit.Open(MailFailures.TransportUnavailable));
        Assert.Equal(MailFailures.TransportUnavailable, circuit.LastErrorCode);
    }
}
