using AiNexus.Features.Identity.Users;

namespace AiNexus.UnitTests.Identity;

public sealed class DisplayNameCacheTests
{
    [Fact]
    public void DisplayNamesAreCachedPerInstance()
    {
        var calls = 0;
        var first = new DisplayNameCache(TimeProvider.System);
        Assert.Equal("甲", first.GetOrAdd("S-1", () => { calls++; return "甲"; }));
        Assert.Equal("甲", first.GetOrAdd("S-1", () => { calls++; return "乙"; }));
        Assert.Equal("乙", new DisplayNameCache(TimeProvider.System).GetOrAdd("S-1", () => { calls++; return "乙"; }));
        Assert.Equal(2, calls);
        for (var i = 0; i < 5000; i++) first.GetOrAdd($"S-{i}", () => "名");
        Assert.Equal("名", first.GetOrAdd("S-4999", () => throw new InvalidOperationException("cached")));
    }
}
