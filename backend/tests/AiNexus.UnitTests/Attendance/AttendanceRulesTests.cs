using AiNexus.Features.Office.Attendance;
using AiNexus.Features.Office.Attendance.Records;

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
        // 補足的例子也由判定算出：09:01:19 到、請 08:30～09:00，等於 08:31:19 到；09:10 到、18:00 走只上了 7 小時 20 分。
        Assert.Contains("工時（上班日 8 小時）", Rule(rules, "表單補足"));
        Assert.Contains("09:01:19 到、請 08:30～09:00 的假，扣表單後應下班 18:01:19", Rule(rules, "表單補足"));
        Assert.Contains("09:10 到、18:00 走，遲到 10 分，但要 40 分的表單才補足", Rule(rules, "表單補足"));
        Assert.Contains("午休 12:00～13:30 不算在內", Rule(rules, "表單補足"));
        Assert.Contains("已補足", Rule(rules, "補足狀態"));
        Assert.Contains("退回結案是承辦人", Rule(rules, "假單與出差單"));
        Assert.Contains("都以秒計", Rule(rules, "早退"));
        Assert.Contains("流程中", Rule(rules, "補足狀態"));
        Assert.Contains("09:00:59 到不算遲到", Rule(AttendanceRules.Describe(new WorkdayOptions { PunchPrecision = PunchPrecision.Minute }), "早退"));
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
        Assert.Contains("工時（上班日 7.5 小時）", Rule(rules, "表單補足"));
        Assert.Contains("09:31:19 到、請 08:00～08:30 的假，扣表單後應下班 18:01:19", Rule(rules, "表單補足"));
        Assert.Contains("09:40 到、17:30 走，遲到 10 分，但要 1 小時 10 分的表單才補足", Rule(rules, "表單補足"));
        Assert.Equal("9.5", AttendanceRules.Hours(9.5));
        Assert.Equal("9", AttendanceRules.Hours(9));
    }

    private static string Rule(AttendanceRulesDto rules, string title) => Assert.Single(rules.Rules, x => x.Title == title).Text;
}
