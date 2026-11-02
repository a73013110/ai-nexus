using System.Globalization;
using AiNexus.Platform.Time;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.UnitTests.Platform;

public sealed class TaipeiTimeTests
{
    // 外部資料庫讀回來的 DateTime 通常是 Unspecified，但 Local 或 Utc 也不能讓建構子拋例外或被換算。
    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public void LocalTimeWithoutAZoneBecomesPlusEightWhateverItsKind(DateTimeKind kind)
    {
        var converted = TaipeiTime.At(new DateTime(2026, 10, 22, 9, 30, 0, kind));

        Assert.Equal(new DateTimeOffset(2026, 10, 22, 9, 30, 0, TimeSpan.FromHours(8)), converted);
        Assert.Equal(TimeSpan.FromHours(8), converted.Offset);
    }

    [Fact]
    public void DateAndTimeOfDayCombineIntoTheSameLocalMoment()
        => Assert.Equal(TaipeiTime.At(new DateTime(2026, 10, 22, 9, 30, 0)), TaipeiTime.At(new DateOnly(2026, 10, 22), new TimeOnly(9, 30)));

    [Fact]
    public void ConvertingKeepsTheInstantAndChangesOnlyTheClockReading()
    {
        var utc = DateTimeOffset.Parse("2026-10-22T16:30:00Z", CultureInfo.InvariantCulture);

        var local = utc.ToTaipei();

        Assert.Equal(utc, local);
        Assert.Equal(new DateOnly(2026, 10, 23), DateOnly.FromDateTime(local.DateTime));
        Assert.Equal(new TimeOnly(0, 30), TimeOnly.FromDateTime(local.DateTime));
    }

    [Theory]
    [InlineData("2026-10-22T15:59:59Z", 22)]
    [InlineData("2026-10-22T16:00:00Z", 23)]
    public void TodayChangesAtTaiwanMidnightNotUtcMidnight(string now, int day)
        => Assert.Equal(new DateOnly(2026, 10, day), TaipeiTime.Today(new FakeTimeProvider(DateTimeOffset.Parse(now, CultureInfo.InvariantCulture))));
}
