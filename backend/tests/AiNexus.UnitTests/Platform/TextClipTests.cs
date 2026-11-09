using AiNexus.Platform.Strings;

namespace AiNexus.UnitTests.Platform;

public sealed class TextClipTests
{
    [Theory]
    [InlineData("abc", 5, "abc")]
    [InlineData("abc", 3, "abc")]
    [InlineData("abcd", 3, "abc")]
    [InlineData("中文字", 2, "中文")]
    [InlineData("abc", 0, "")]
    public void KeepsAtMostTheLimit(string text, int max, string expected)
        => Assert.Equal(expected, text.Clip(max));

    [Fact]
    public void NeverLeavesHalfASurrogatePair()
    {
        var clipped = "a😀b".Clip(2);

        Assert.Equal("a", clipped);
    }

    [Fact]
    public void KeepsAWholePairThatFits()
        => Assert.Equal("a😀", "a😀b".Clip(3));
}
