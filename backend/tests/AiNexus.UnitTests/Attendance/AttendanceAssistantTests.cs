using AiNexus.Features.Office.Attendance;
using AiNexus.Features.Office.Attendance.Assistant;
using AiNexus.Features.Office.Attendance.Forms;
using AiNexus.Features.Office.Attendance.Records;
using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.Time;

namespace AiNexus.UnitTests.Attendance;

/// <summary>出勤交給助理的事實：與畫面同一份資料，重要的在前（放不下時從後面捨去），逐日的結論與判定說明同一句。</summary>
public sealed class AttendanceAssistantTests
{
    private static readonly WorkdayOptions Rules = new();
    private static readonly DateOnly Today = new(2026, 10, 22);

    private static DateTimeOffset At(int day, int hour, int minute, int second = 0) => TaipeiTime.At(new(2026, 10, day), new TimeOnly(hour, minute, second));

    private static AttendanceRecordDto Day(int day, DateTimeOffset? clockIn, DateTimeOffset? clockOut, AttendanceDay? source = null, IReadOnlyList<GdForm>? forms = null)
    {
        var date = new DateOnly(2026, 10, day);
        IReadOnlyList<FormPeriod> periods = [.. (forms ?? []).Select(x => new FormPeriod(x.StartsAt, x.EndsAt, x.Approved))];
        var result = AttendanceEvaluator.Evaluate(source ?? new(date, AttendanceDayType.FullDay, clockIn, clockOut, periods), Rules, Today);
        return new(date, AttendanceDayType.FullDay, null, result.Status, result.Reason, result.ClockIn, result.ClockOut, result.ExpectedClockOut,
            result.LateSeconds, result.EarlyLeaveSeconds, result.FormState, result.FormSeconds, result.AdjustedClockOut, result.Coverage, result.ShortSeconds,
            result.Gaps, forms ?? []);
    }

    private static AttendanceRecordsDto Records(params AttendancePersonDto[] people)
        => new(new(2026, 10, 19), new(2026, 10, 22), Today, true, true, people.Length, 0, people);

    private static AttendancePersonDto Person(string name, params AttendanceRecordDto[] days)
        => new(Guid.NewGuid(), null, name, name.ToLowerInvariant(), true, false, days);

    [Fact]
    public void RangeAndRulesComeFirstThenPeopleThenTheDaysToFollowUp()
    {
        var pending = new GdForm("L-9", GdFormKind.Leave, "特休", At(20, 8, 30), At(20, 10, 0), GdFormFlowOver.Send, "林主任", "核准關卡");
        var records = Records(
            Person("Bob",
                Day(19, At(19, 8, 30), At(19, 18, 0)),
                Day(20, At(20, 9, 40), At(20, 18, 0), forms: [pending]),
                Day(21, null, null),
                Day(22, At(22, 8, 30), null)),
            Person("Carol", Day(19, At(19, 8, 30), At(19, 18, 0))));

        var grounding = AttendanceAssistant.Records(records, Rules);

        Assert.Equal("2026/10/19–10/22 · 2 人", grounding.Scope);
        var labels = grounding.Facts.Select(x => x.Label).ToList();
        Assert.Equal("查詢範圍", labels[0]);
        var rules = AttendanceRules.Describe(Rules).Rules.Count;
        Assert.All(labels.Skip(1).Take(rules), x => Assert.StartsWith("規則：", x, StringComparison.Ordinal));
        Assert.Equal(["Bob（bob）", "Carol（carol）", "Bob 10/21（三）", "Bob 10/20（二）"], labels.Skip(1 + rules));

        // 今天不算進彙總也不逐日列出；遲到與未補足各自計算。
        Assert.Equal("已結算 3 天：正常 1 天、遲到 1 天、異常 1 天；未補足 1 天（共差 8 小時）、流程中 1 天。", grounding.Facts[1 + rules].Text);
        var late = grounding.Facts[^1].Text;
        Assert.StartsWith("上班 09:40:00、下班 18:00:00、應下班 18:00:00。遲到：", late, StringComparison.Ordinal);
        Assert.EndsWith("流程中的單 L-9（特休）停在 林主任（核准關卡）。", late, StringComparison.Ordinal);
        Assert.StartsWith("沒有刷卡。", grounding.Facts[^2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ADayExplanationStartsWithTheConclusionAndEndsWithTheRulesItRelied()
    {
        var day = Day(20, At(20, 9, 40), At(20, 18, 0));
        var person = Person("Bob", day);
        var explanation = AttendanceExplanation.Explain(person, day, Rules, Today);

        var result = AttendanceAssistant.Explanation(person, explanation, Rules);

        Assert.Equal("Bob 10/20（二）", result.Scope);
        Assert.Equal(("Bob 10/20（二）", "結論：" + explanation.Conclusion), (result.Facts[0].Label, result.Facts[0].Text));
        Assert.Equal(explanation.Steps.Count, result.Facts.Count(x => x.Label.StartsWith("Bob", StringComparison.Ordinal)) - 1);
        Assert.Equal(explanation.Rules.Select(x => "規則：" + x).Order(), result.Facts.Where(x => x.Label.StartsWith("規則：", StringComparison.Ordinal)).Select(x => x.Label).Order());
    }

    [Theory]
    [InlineData("bob", true)]
    [InlineData("BOB", true)]
    [InlineData("Bo", false)]
    public void PeopleAreMatchedByTheirWholeAccountOrName(string text, bool matches)
        => Assert.Equal(matches, AttendanceAssistant.Matches(Person("Bob"), text));
}
