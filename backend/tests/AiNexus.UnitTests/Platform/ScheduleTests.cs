using AiNexus.Platform.Scheduling;

namespace AiNexus.UnitTests.Platform;

public sealed class ScheduleTests
{
    [Theory]
    [InlineData("00:05:00", "interval", "每 5 分鐘")]
    [InlineData("1.00:00:00", "interval", "每 1 天")]
    [InlineData("30 3 * * *", "cron", "每天 03:30")]
    [InlineData("0 8 * * 1-5", "cron", "cron：0 8 * * 1-5")]
    public void ASettingIsReadAsAnIntervalOrCron(string value, string kind, string description)
    {
        var schedule = Schedule.Parse(value);
        Assert.NotNull(schedule);
        Assert.Equal((kind, description), (schedule.Kind, schedule.Description));
    }

    [Theory]
    [InlineData("5")]
    [InlineData("00:00:05")]
    [InlineData("32.00:00:00")]
    [InlineData("every hour")]
    public void AnUnreadableOrOutOfRangeSettingIsRejected(string value) => Assert.Null(Schedule.Parse(value));

    [Fact]
    public void CronListsItsFixedTimesInTaipeiWithinTheWindow()
    {
        var after = new DateTimeOffset(2026, 11, 9, 5, 0, 0, TimeSpan.Zero);
        Assert.Equal(
            [new DateTimeOffset(2026, 11, 9, 19, 30, 0, TimeSpan.Zero)],
            Schedule.Parse("30 3 * * *")!.Occurrences(after, after.AddHours(24)));
    }

    [Fact]
    public void SchedulesWithoutFixedTimesListNoOccurrences()
    {
        var after = new DateTimeOffset(2026, 11, 9, 5, 0, 0, TimeSpan.Zero);
        Assert.Empty(Schedule.Parse("00:05:00")!.Occurrences(after, after.AddHours(24)));
        Assert.Empty(Schedule.AtStartup(TimeSpan.FromMinutes(1)).Occurrences(after, after.AddHours(24)));
    }
}
