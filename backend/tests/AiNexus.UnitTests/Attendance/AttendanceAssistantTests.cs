using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Office.Attendance;
using AiNexus.Features.Office.Attendance.Assistant;
using AiNexus.Features.Office.Attendance.Forms;
using AiNexus.Features.Office.Attendance.Records;
using AiNexus.Features.Platform.Assistant;
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
        Assert.All(grounding.Facts.Skip(1).Take(rules), x => Assert.Equal(AssistantFactKind.Rule, x.Kind));
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
        Assert.Equal(explanation.Rules.Order(), result.Facts.Where(x => x.Kind == AssistantFactKind.Rule).Select(x => x.Label).Order());
    }

    private static readonly DateTimeOffset Now = At(22, 10, 0);

    private static AttendanceSetupHolding Holding(string card, int from, int? to = null, bool hidden = false)
        => new(card, hidden, At(from, 0, 0), to is { } day ? At(day, 0, 0) : null, null);

    private static AttendanceSetup Setup(bool? active, params AttendanceSetupHolding[] holdings)
        => new(active is null ? null : Guid.NewGuid(), "Bob", active is null ? null : "bob", active, holdings);

    [Fact]
    public void ADisabledAccountThatStillHoldsACardIsACheckThatLinksToTheCardPage()
    {
        var result = AttendanceAssistant.Setup("bob", [Setup(false, Holding("A1", 1))], Now);

        var check = result.Facts[0];
        Assert.Equal((AssistantFactKind.Check, "Bob（bob）"), (check.Kind, check.Label));
        Assert.StartsWith("帳號已停用，卡號 A1 的持有期間沒有結束", check.Text, StringComparison.Ordinal);
        Assert.Equal(new AssistantLink(FeatureCatalog.AttendanceCards, "search=A1"), check.Link);
        Assert.Contains(result.Facts, x => x is { Label: "Bob（bob）", Text: "帳號已停用（離職）。" });
        Assert.Contains(result.Facts, x => x is { Label: "Bob（bob） 卡號 A1", Text: "2026/10/01 起，持有中（沒有結束時間）。" });
        // 名單怎麼組成放在最後，放不下時先捨去。
        var rules = result.Facts.SkipWhile(x => x.Kind != AssistantFactKind.Rule).ToList();
        Assert.All(rules, x => Assert.Equal(AssistantFactKind.Rule, x.Kind));
        Assert.Equal(["誰會列出", "帳號停用", "隱藏的卡"], rules.Select(x => x.Label).Intersect(["誰會列出", "帳號停用", "隱藏的卡"]));
    }

    [Fact]
    public void HiddenCardsAndAccountsWithoutCardsAreSettingsNotChecks()
    {
        // 隱藏的卡是刻意的設定：寫在持有期間上，由〈規則〉說明為什麼看整份名單時不見。
        var hidden = AttendanceAssistant.Setup("bob", [Setup(true, Holding("A1", 1, hidden: true))], Now);
        Assert.DoesNotContain(hidden.Facts, x => x.Kind == AssistantFactKind.Check);
        Assert.Contains(hidden.Facts, x => x.Text.Contains("這張卡設為在出勤紀錄隱藏", StringComparison.Ordinal));

        var cardless = AttendanceAssistant.Setup("bob", [Setup(true)], Now);
        Assert.DoesNotContain(cardless.Facts, x => x.Kind == AssistantFactKind.Check);
        Assert.EndsWith("沒有任何出勤卡的持有紀錄，所以不在出勤名單上。", cardless.Facts[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnActiveAccountWhoseLastCardEndedIsACheck()
    {
        var ended = AttendanceAssistant.Setup("bob", [Setup(true, Holding("A1", 1, 15))], Now);
        Assert.Equal(AssistantFactKind.Check, ended.Facts[0].Kind);
        Assert.Contains("上次是卡號 A1，2026/10/15 結束", ended.Facts[0].Text, StringComparison.Ordinal);

        // 最後一張卡是隱藏的，本來就不在名單上，不提示（與帳號待辦相同）。
        Assert.DoesNotContain(AttendanceAssistant.Setup("bob", [Setup(true, Holding("A1", 1, 15, hidden: true))], Now).Facts, x => x.Kind == AssistantFactKind.Check);
    }

    [Fact]
    public void NobodyFoundStillAnswersWithTheScopeOfTheSearch()
    {
        var result = AttendanceAssistant.Setup("dave", [], Now);

        Assert.Equal("「dave」· 查無此人", result.Scope);
        Assert.StartsWith("在你的資料範圍內找不到", result.Facts[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheListCarriesChecksOnlyForPeopleOnIt()
    {
        var bob = Person("Bob", Day(19, null, null));
        var stillHeld = new AttendanceSetup(bob.UserId, "Bob", "bob", false, [Holding("A1", 1)]);
        var elsewhere = new AttendanceSetup(Guid.NewGuid(), "Dave", "dave", false, [Holding("A9", 1)]);

        var grounding = AttendanceAssistant.Records(Records(bob), Rules, [stillHeld, elsewhere], Now);

        Assert.Equal(AssistantFactKind.Check, grounding.Facts[1].Kind);
        Assert.Equal("Bob（bob）", grounding.Facts[1].Label);
        Assert.DoesNotContain(grounding.Facts, x => x.Label.StartsWith("Dave", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("bob", true)]
    [InlineData("BOB", true)]
    [InlineData("Bo", false)]
    public void PeopleAreMatchedByTheirWholeAccountOrName(string text, bool matches)
        => Assert.Equal(matches, AttendanceAssistant.Matches(Person("Bob"), text));
}
