using AiNexus.Features.Chat;

namespace AiNexus.UnitTests.Chat;

public sealed class SubscriptionLimitsTests
{
    [Fact]
    public void SubscriptionCapacityIsReleasedWhenClientsDisconnect()
    {
        var limits = new SubscriptionLimits(); var owner = Guid.NewGuid();
        using var first = limits.TryAcquire(owner); using (limits.TryAcquire(owner)) Assert.Null(limits.TryAcquire(owner));
        using var replacement = limits.TryAcquire(owner);
        Assert.NotNull(replacement);
    }
}
