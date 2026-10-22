using AiNexus.Features.Staff.Attendance;
using AiNexus.Features.Staff.Attendance.Records;

namespace AiNexus.UnitTests.Attendance;

public sealed class AttendanceRulesTests
{
    [Fact]
    public void DefaultRulesDescribeTheConfiguredTimesWithExamplesTheEvaluatorAgreesWith()
    {
        var rules = AttendanceRules.Describe(new WorkdayOptions());

        Assert.Equal([AttendanceDayType.FullDay, AttendanceDayType.MorningOff, AttendanceDayType.AfternoonOff], rules.Schedules.Select(x => x.DayType));
        var full = rules.Schedules[0];
        Assert.Equal((new TimeOnly(8, 30), new TimeOnly(18, 0), new TimeOnly(8, 0), new TimeOnly(9, 0), 9.5),
            (full.ScheduledStart, full.ScheduledEnd, full.FlexStart, full.FlexEnd, full.WorkHours));
        // 例子由判定算出：08:00 前到以 08:00 計＋9.5 小時；08:30 到＋9.5 小時；09:10 到是遲到，應下班改為表定 18:00。
        Assert.Contains("08:00 前到，應下班 17:30", Rule(rules, "彈性上班"));
        Assert.Contains("08:30 到，應下班 18:00", Rule(rules, "彈性上班"));
        Assert.Contains("09:10 到，遲到 10 分，應下班 18:00", Rule(rules, "遲到"));
        Assert.Equal(rules.Rules.Count, rules.Rules.Select(x => x.Title).Distinct().Count());
    }

    [Fact]
    public void ChangedSettingsChangeTheDescription()
    {
        var options = new WorkdayOptions();
        options.FullDay.FlexStart = new(7, 30);
        options.FullDay.ScheduledStart = new(8, 0);
        options.FullDay.FlexEnd = new(9, 30);
        options.FullDay.ScheduledEnd = new(17, 30);
        options.FullDay.WorkHours = 9;

        var rules = AttendanceRules.Describe(options);

        Assert.Equal(9, rules.Schedules[0].WorkHours);
        Assert.Contains("07:30 前到，應下班 16:30", Rule(rules, "彈性上班"));
        Assert.Contains("09:40 到，遲到 10 分，應下班 17:30", Rule(rules, "遲到"));
        Assert.Equal("9.5", AttendanceRules.Hours(9.5));
        Assert.Equal("9", AttendanceRules.Hours(9));
    }

    private static string Rule(AttendanceRulesDto rules, string title) => Assert.Single(rules.Rules, x => x.Title == title).Text;
}
