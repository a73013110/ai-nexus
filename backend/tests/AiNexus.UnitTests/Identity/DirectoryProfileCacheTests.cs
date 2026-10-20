using AiNexus.Features.Admin.Identity.Users;

namespace AiNexus.UnitTests.Identity;

public sealed class DirectoryProfileCacheTests
{
    [Fact]
    public void ProfilesAreCachedPerInstance()
    {
        var calls = 0;
        var first = new DirectoryProfileCache(TimeProvider.System);
        Assert.Equal("甲", first.GetOrAdd("S-1", () => { calls++; return new("甲", "a@company.com"); }).DisplayName);
        Assert.Equal("a@company.com", first.GetOrAdd("S-1", () => { calls++; return new("乙", null); }).Email);
        Assert.Equal("乙", new DirectoryProfileCache(TimeProvider.System).GetOrAdd("S-1", () => { calls++; return new("乙", null); }).DisplayName);
        Assert.Equal(2, calls);
        for (var i = 0; i < 5000; i++) first.GetOrAdd($"S-{i}", () => new("名", null));
        Assert.Equal("名", first.GetOrAdd("S-4999", () => throw new InvalidOperationException("cached")).DisplayName);
    }
}
