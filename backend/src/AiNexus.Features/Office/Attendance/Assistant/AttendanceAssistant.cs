using System.Globalization;
using AiNexus.Features.Office.Attendance.Records;
using AiNexus.Features.Platform.Assistant;
using AiNexus.Platform.Time;

namespace AiNexus.Features.Office.Attendance.Assistant;

/// <summary>
/// 出勤交給助理的資料（純函式）：把查詢結果寫成模型讀得懂、可以逐條引用的事實。三個工具共用：
/// <see cref="AttendanceRecordsTool"/>（名單）、<see cref="AttendanceDayTool"/>（一天的判定過程）、<see cref="AttendanceRulesTool"/>（規則）。
/// 數字與判定都來自 <see cref="AttendanceEvaluator"/>（逐日的結論由 <see cref="AttendanceExplanation"/> 寫），這裡不另算一套。
/// </summary>
public static class AttendanceAssistant
{
    /// <summary>出勤工具共用的回答規則（系統指令只寫一次）。</summary>
    public const string Guidance = """
        出勤：
        - 「未補足」是有遲到、早退或未刷卡的缺口，核准的表單不夠抵；「流程中」是表單還在簽核，核准後就補足；「已補足」是核准的表單已抵完。
        - 問到某一天為什麼是這個結果，照那一天的判定過程與「規則：」開頭的條文說明；問到怎麼補，說明缺多少時數、要補哪一種表單，不替使用者送單。
        - 今天還沒結算，不評論今天的出勤。
        - 問到不在資料裡的人或日期，先說明查詢範圍，再建議換條件查。
        """;

    /// <summary>
    /// 名單：依重要性排序，模型放不下時從後面捨去：範圍與資料來源 → 規則 → 每人彙總 → 逐日明細（未補足、流程中、其他異常、已補足）。
    /// 沒有缺口的正常日只算進彙總，不逐日列出。
    /// </summary>
    public static AssistantToolResult Records(AttendanceRecordsDto records, WorkdayOptions options)
    {
        var people = records.People;
        List<AssistantFact> facts = [new("查詢範圍", Range(records))];
        facts.AddRange(Rules(options));
        facts.AddRange(people.Select(x => new AssistantFact(Name(x), Summary(x, records.Today))));

        var days = people
            .SelectMany(person => person.Days.Where(day => Notable(day, records.Today)).Select(day => (person, day)))
            .OrderBy(x => Priority(x.day))
            .ThenBy(x => x.day.Date)
            .Select(x => new AssistantFact($"{x.person.DisplayName} {Date(x.day.Date)}", Day(x.person, x.day, options, records.Today)));
        facts.AddRange(days);

        return new($"{records.From:yyyy/MM/dd}–{records.To:MM/dd} · {records.Total} 人", facts);
    }

    /// <summary>一個人一天的判定過程：結論在前，接著依推論順序的每一步，最後是依據的規則條文。</summary>
    public static AssistantToolResult Explanation(AttendancePersonDto person, AttendanceExplanationDto explanation, WorkdayOptions options)
    {
        var subject = $"{person.DisplayName} {Date(explanation.Date)}";
        List<AssistantFact> facts = [new(subject, "結論：" + explanation.Conclusion)];
        facts.AddRange(explanation.Steps.Select(step =>
            new AssistantFact(step.At is { } at ? $"{subject} {at.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}" : subject, step.Text)));
        facts.AddRange(Rules(options).Where(rule => explanation.Rules.Any(title => rule.Label == "規則：" + title)));
        return new(subject, facts);
    }

    public static IEnumerable<AssistantFact> Rules(WorkdayOptions options)
        => AttendanceRules.Describe(options).Rules.Select(x => new AssistantFact("規則：" + x.Title, x.Text.ReplaceLineEndings(" ")));

    /// <summary>模型給的人：帳號、姓名或卡號，完全相同才算（不分大小寫）；名單上可能有同名的人，由呼叫端處理。</summary>
    public static bool Matches(AttendancePersonDto person, string text)
        => string.Equals(person.Account, text, StringComparison.OrdinalIgnoreCase)
            || string.Equals(person.DisplayName, text, StringComparison.OrdinalIgnoreCase)
            || string.Equals(person.CardNumber, text, StringComparison.OrdinalIgnoreCase);

    private static string Range(AttendanceRecordsDto records)
    {
        var text = $"{records.From:yyyy/MM/dd}～{records.To:yyyy/MM/dd}，共 {records.Total} 人；今天是 {records.Today:yyyy/MM/dd}，今天的出勤晚上才結算。";
        if (!records.CalendarAvailable) text += "公文系統的行事曆讀不到：平日一律無法判定。";
        if (!records.FormsAvailable) text += "公文系統的表單讀不到：表單一律無法取得。";
        return text;
    }

    private static string Name(AttendancePersonDto person)
        => person.Account is { } account ? $"{person.DisplayName}（{account}）"
            : person.CardNumber is { } card ? $"{person.DisplayName}（卡號 {card}，未綁帳號）"
            : person.DisplayName;

    /// <summary>一個人在區間內的天數：遲到、早退各自算（同一天可能都有），補足狀態另外算；零的不寫。</summary>
    private static string Summary(AttendancePersonDto person, DateOnly today)
    {
        var days = person.Days.Where(x => x.Date != today).ToList();
        var normal = days.Count(x => x.Status == AttendanceStatus.Present && x.LateSeconds == 0 && x.EarlyLeaveSeconds == 0);
        var statuses = Join(
            Count("正常", normal),
            Count("遲到", days.Count(x => x.LateSeconds > 0)),
            Count("早退", days.Count(x => x.EarlyLeaveSeconds > 0)),
            Count("異常", days.Count(x => x.Status == AttendanceStatus.Anomaly)),
            Count("請假", days.Count(x => x.Status == AttendanceStatus.OnLeave)),
            Count("無法判定", days.Count(x => x.Status == AttendanceStatus.Undetermined)),
            Count("放假", days.Count(x => x.Status == AttendanceStatus.DayOff)));
        var unexplained = days.Where(x => x.Coverage == GapCoverage.Unexplained).ToList();
        var coverage = Join(
            unexplained.Count > 0 ? $"未補足 {unexplained.Count} 天（共差 {AttendanceRules.Duration(unexplained.Sum(x => x.ShortSeconds))}）" : null,
            Count("流程中", days.Count(x => x.Coverage == GapCoverage.Pending)),
            Count("已補足", days.Count(x => x.Coverage == GapCoverage.Covered)));
        var text = $"已結算 {days.Count} 天" + (statuses.Length > 0 ? "：" + statuses : "") + (coverage.Length > 0 ? "；" + coverage : "") + "。";
        if (!person.FormsAvailable) text += "對不到公文系統帳號，表單無法取得。";
        return text;
    }

    /// <summary>值得逐日列出的日子：有缺口或判成異常、無法判定的日子（今天不算）。</summary>
    private static bool Notable(AttendanceRecordDto day, DateOnly today)
        => day.Date != today && (day.Coverage is not null || day.Status is AttendanceStatus.Anomaly or AttendanceStatus.Undetermined);

    private static int Priority(AttendanceRecordDto day) => day.Coverage switch
    {
        GapCoverage.Unexplained => 0,
        GapCoverage.Pending => 1,
        GapCoverage.Covered => 3,
        _ => 2
    };

    private static string Day(AttendancePersonDto person, AttendanceRecordDto day, WorkdayOptions options, DateOnly today)
    {
        var punches = Join(
            day.ClockIn is { } clockIn ? "上班 " + Clock(clockIn) : null,
            day.ClockOut is { } clockOut && clockOut != day.ClockIn ? "下班 " + Clock(clockOut) : null,
            day.ExpectedClockOut is { } expected ? "應下班 " + Clock(expected) : null);
        var text = (punches.Length > 0 ? punches + "。" : "沒有刷卡。") + AttendanceExplanation.Explain(person, day, options, today).Conclusion;
        foreach (var form in day.Forms.Where(x => !x.Approved && x.CurrentSigner is not null))
            text += $"流程中的單 {form.DesNo}（{form.Title}）停在 {form.CurrentSigner}" + (form.CurrentStep is { } step ? $"（{step}）" : "") + "。";
        return text;
    }

    private static string Date(DateOnly date) => $"{date:MM/dd}（{"日一二三四五六"[(int)date.DayOfWeek]}）";

    private static string Clock(DateTimeOffset value) => value.ToTaipei().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static string? Count(string label, int days) => days > 0 ? $"{label} {days} 天" : null;

    private static string Join(params string?[] parts) => string.Join("、", parts.Where(x => x is not null));
}
