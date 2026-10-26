using AiNexus.Features.Office.Attendance;
using Microsoft.Extensions.Configuration;

namespace AiNexus.UnitTests.Attendance;

public sealed class WorkdayOptionsTests
{
    private static AttendanceOptions Bind(Dictionary<string, string?> values)
    {
        var options = new AttendanceOptions();
        new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection(AttendanceOptions.Section)
            .Bind(options, x => x.ErrorOnUnknownConfiguration = true);
        return options;
    }

    private static string? Failure(Dictionary<string, string?> values)
    {
        var result = new AttendanceOptionsValidator().Validate(null, Bind(values));
        return result.Failed ? result.FailureMessage : null;
    }

    [Fact]
    public void TheDefaultsFollowTheCompanyRulesAndValidate()
    {
        var workday = new AttendanceOptions().Workday;

        Assert.Equal((new TimeOnly(8, 30), new TimeOnly(18, 0), new TimeOnly(8, 0), new TimeOnly(9, 0), 9.5),
            (workday.FullDay.ScheduledStart, workday.FullDay.ScheduledEnd, workday.FullDay.FlexStart, workday.FullDay.FlexEnd, workday.FullDay.WorkHours));
        Assert.Equal((new TimeOnly(13, 30), new TimeOnly(18, 0), 4.5), (workday.MorningOff.ScheduledStart, workday.MorningOff.ScheduledEnd, workday.MorningOff.WorkHours));
        Assert.Equal((new TimeOnly(8, 30), new TimeOnly(12, 0), 3.5), (workday.AfternoonOff.ScheduledStart, workday.AfternoonOff.ScheduledEnd, workday.AfternoonOff.WorkHours));
        // 只有全天有午休；半天的日型沒有。
        Assert.Equal((new TimeOnly(12, 0), new TimeOnly(13, 30)), (workday.FullDay.BreakStart, workday.FullDay.BreakEnd));
        Assert.Equal((null, null), (workday.MorningOff.BreakStart, workday.AfternoonOff.BreakEnd));
        Assert.Null(Failure([]));
    }

    [Fact]
    public void TimesAndHoursBindFromText()
    {
        var rule = Bind(new() { ["Attendance:Workday:FullDay:FlexEnd"] = "09:30", ["Attendance:Workday:FullDay:WorkHours"] = "9.25" }).Workday.FullDay;

        Assert.Equal((new TimeOnly(9, 30), 9.25), (rule.FlexEnd, rule.WorkHours));
    }

    [Theory]
    [InlineData("Attendance:Workday:FullDay:FlexStart", "08:45", "FlexStart <= ScheduledStart <= FlexEnd")]
    [InlineData("Attendance:Workday:MorningOff:FlexEnd", "13:00", "FlexStart <= ScheduledStart <= FlexEnd")]
    [InlineData("Attendance:Workday:AfternoonOff:ScheduledEnd", "08:00", "ScheduledEnd must be later than ScheduledStart")]
    [InlineData("Attendance:Workday:FullDay:WorkHours", "0", "WorkHours")]
    [InlineData("Attendance:Workday:MorningOff:WorkHours", "11", "FlexEnd + WorkHours must stay within the same day")]
    [InlineData("Attendance:Workday:MorningOff:BreakStart", "15:00", "BreakStart and BreakEnd must be set together")]
    [InlineData("Attendance:Workday:FullDay:BreakEnd", "11:00", "FlexEnd < BreakStart < BreakEnd <= ScheduledEnd")]
    [InlineData("Attendance:Workday:FullDay:BreakStart", "09:00", "FlexEnd < BreakStart < BreakEnd <= ScheduledEnd")]
    [InlineData("Attendance:Workday:FullDay:WorkHours", "5", "BreakEnd must be earlier than FlexStart + WorkHours")]
    public void InvalidRulesFailAndNameTheKey(string key, string value, string message)
    {
        var failure = Failure(new() { [key] = value });

        Assert.NotNull(failure);
        Assert.Contains(message, failure, StringComparison.Ordinal);
        // 訊息要指出是哪一個日型的規則（例如 Workday.MorningOff），不然三份規則的欄位同名，看不出該改哪裡。
        Assert.Contains("Workday." + key.Split(':')[2], failure, StringComparison.Ordinal);
    }
}
