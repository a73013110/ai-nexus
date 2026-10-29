using AiNexus.Features.Admin.Releases;

namespace AiNexus.UnitTests.Releases;

public sealed class ReleaseVersionTests
{
    [Fact]
    public void PreviewsComeBeforeTheReleaseAndDevelopmentBuildsAfterIt()
    {
        string[] ordered = ["0.0.0-dev", "2026.03.9", "2026.03.10-preview", "2026.03.10", "2026.03.10-dev.1", "2026.03.10-dev.12", "2026.03.11", "2026.11.0", "2027.01.0"];
        var parsed = ordered.Select(x => ReleaseVersion.Parse(x)!).ToList();
        Assert.Equal(parsed, parsed.Order());
        Assert.True(ReleaseVersion.Parse("2026.03.10")! >= ReleaseVersion.Parse("2026.3.10")!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v2026.03.1")]
    [InlineData("1.0.0.0")]
    [InlineData("2026.03.1-beta")]
    public void TextThatIsNotAVersionIsUnknownRatherThanOldest(string? text) => Assert.Null(ReleaseVersion.Parse(text));
}
