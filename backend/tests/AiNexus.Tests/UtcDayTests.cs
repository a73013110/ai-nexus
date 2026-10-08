using System.Globalization;
using AiNexus.Platform.Time;
using Xunit;

namespace AiNexus.Tests;

public sealed class UtcDayTests
{
    [Theory]
    [InlineData("2026-10-06T07:59:59+08:00", "2026-10-05T00:00:00Z")]
    [InlineData("2026-10-06T08:00:00+08:00", "2026-10-06T00:00:00Z")]
    [InlineData("2026-10-05T19:59:59-04:00", "2026-10-05T00:00:00Z")]
    [InlineData("2026-10-05T20:00:00-04:00", "2026-10-06T00:00:00Z")]
    public void ResetBoundariesAreAbsoluteUtcRegardlessOfInputAndServerOffsets(string instant, string expected)
    {
        var start = UtcDay.Start(DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture));
        Assert.Equal(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture), start);
        Assert.Equal(TimeSpan.Zero, start.Offset);
    }
}
