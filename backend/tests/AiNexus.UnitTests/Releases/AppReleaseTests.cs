using System.Text;
using AiNexus.Features.Admin.Releases;

namespace AiNexus.UnitTests.Releases;

public sealed class AppReleaseTests
{
    [Fact]
    public void TheVersionComesFromTheAssemblyAndTheNotesFromThePackage()
    {
        var release = new AppRelease("2026.11.4+abc123", Encoding.UTF8.GetBytes("""
            {"version":"2026.11.4","builtAt":"2026-11-02T08:00:00Z","minimumDatabaseVersion":"2026.11.3","changes":[{"type":"fix","scope":"","subject":"登入逾時後保留草稿"}]}
            """));
        Assert.Equal(("2026.11.4", "abc123", true, "2026.11.3"), (release.Version, release.Revision, release.Packaged, release.MinimumDatabaseVersion));
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero), release.BuiltAt);
        Assert.Equal("登入逾時後保留草稿", Assert.Single(release.Changes).Subject);
    }

    [Fact]
    public void AMissingOrDamagedPackageFileOnlyLeavesTheNotesOut()
    {
        var damaged = new AppRelease("2026.11.4", Encoding.UTF8.GetBytes("{not json"));
        Assert.Equal(("2026.11.4", (string?)null, false, (string?)null), (damaged.Version, damaged.Revision, damaged.Packaged, damaged.MinimumDatabaseVersion));
        Assert.False(new AppRelease("0.0.0-dev", null).Packaged);
    }
}
