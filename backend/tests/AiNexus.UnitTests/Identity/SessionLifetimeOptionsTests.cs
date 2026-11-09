using AiNexus.Features.Admin.Identity.Sessions;

namespace AiNexus.UnitTests.Identity;

public sealed class SessionLifetimeOptionsTests
{
    [Fact]
    public void DefaultsAreValid() => Assert.True(new SessionLifetimeOptionsValidator().Validate(null, new()).Succeeded);

    [Fact]
    public void OneMinuteIdleLockIsAllowed() => Assert.True(new SessionLifetimeOptionsValidator().Validate(null, new() { IdleLockMinutes = 1 }).Succeeded);

    [Fact]
    public void HardLimitCannotArriveBeforeTheLock()
    {
        var result = new SessionLifetimeOptionsValidator().Validate(null, new() { ReauthenticationHours = 8, HardLimitHours = 4 });
        Assert.True(result.Failed);
        Assert.Contains(nameof(SessionLifetimeOptions.HardLimitHours), result.FailureMessage, StringComparison.Ordinal);
    }
}
