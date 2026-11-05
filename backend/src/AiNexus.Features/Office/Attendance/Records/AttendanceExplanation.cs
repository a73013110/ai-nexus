using System.Globalization;
using AiNexus.Platform.Time;
using static AiNexus.Features.Office.Attendance.Records.AttendanceRules;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 一天的判定說明：結論是怎麼一步步得出的（刷卡、應下班、上班時數、表單）。出勤紀錄點開狀態時顯示，之後頁內助理也以它為事實來源。
/// <paramref name="Schedule"/> 是當天日型的規則（放假與日型未知為 null）；<paramref name="Steps"/> 依推論順序排列；<paramref name="Conclusion"/> 是一句結論；
/// <paramref name="Rules"/> 是依據的條文標題（<see cref="AttendanceRules.Titles"/>），畫面據此列出規則說明的同一條。狀態、補足與差額同出勤紀錄，畫面用來上色。
/// </summary>
public sealed record AttendanceExplanationDto(DateOnly Date, AttendanceDayType DayType, AttendanceStatus Status, AttendanceReason? Reason, GapCoverage? Coverage,
    int ShortSeconds, AttendanceScheduleDto? Schedule, IReadOnlyList<AttendanceExplanationStep> Steps, string Conclusion, IReadOnlyList<string> Rules);

/// <summary>說明的一步：<paramref name="At"/> 是這一步的時間（台灣時間，精確到秒），沒有特定時間的步驟為 null。</summary>
public sealed record AttendanceExplanationStep(AttendanceExplanationStepKind Kind, TimeOnly? At, string Text);

/// <summary>說明一步的種類，畫面據此決定圓點的樣子。</summary>
public enum AttendanceExplanationStepKind
{
    /// <summary>刷卡本身。</summary>
    Punch = 0,

    /// <summary>造成缺口的刷卡（遲到、早退、沒刷卡、缺一筆）；顏色跟著補足狀態。</summary>
    Gap = 1,

    /// <summary>由規則推得的時間或時數（應下班、該上的工時）。</summary>
    Rule = 2,

    /// <summary>表單抵掉的時數與扣表單後的應下班。</summary>
    Form = 3,

    /// <summary>補充說明（放假、無法判定、整分計算）。</summary>
    Note = 4
}

/// <summary>
/// 由 <see cref="AttendanceEvaluator"/> 的判定結果寫出說明（純函式）。數字全部來自判定本身（含 <see cref="AttendanceEvaluation.RequiredSeconds"/> 等中間值），
/// 這裡只決定怎麼說，不另算一套；用語與 <see cref="AttendanceRules"/> 的條文一致，改判定時兩處一起改（<c>AttendanceExplanationTests</c>）。
/// </summary>
public static class AttendanceExplanation
{
    public static AttendanceExplanationDto Explain(AttendanceDay day, WorkdayOptions options, DateOnly today)
    {
        var result = AttendanceEvaluator.Evaluate(day, options, today);
        var rule = AttendanceEvaluator.RuleOf(day.DayType, options);
        var story = new Story();
        var conclusion = Narrate(story, day, result, rule, options);
        return new(day.Date, day.DayType, result.Status, result.Reason, result.Coverage, result.ShortSeconds,
            rule is null ? null : AttendanceScheduleDto.Of(day.DayType, rule), story.Steps, conclusion, [.. story.Rules]);
    }

    /// <summary>
    /// 出勤紀錄回應裡的一天：由回應還原判定的輸入再說明，所以與畫面同一份資料。
    /// 今天的下班刷卡回應裡本來就沒有，還原後同樣不判定；表單無法取得的人沒有表單（null），不是沒有表單。
    /// </summary>
    public static AttendanceExplanationDto Explain(AttendancePersonDto person, AttendanceRecordDto record, WorkdayOptions options, DateOnly today)
    {
        IReadOnlyList<FormPeriod>? forms = person.FormsAvailable ? [.. record.Forms.Select(x => new FormPeriod(x.StartsAt, x.EndsAt, x.Approved))] : null;
        return Explain(new AttendanceDay(record.Date, record.DayType, record.ClockIn, record.ClockOut, forms), options, today);
    }

    private static string Narrate(Story story, AttendanceDay day, AttendanceEvaluation result, WorkdayRule? rule, WorkdayOptions options)
    {
        var (clockIn, clockOut) = (Clock(result.ClockIn), Clock(result.ClockOut));
        var single = result.ClockIn is not null && result.ClockIn == result.ClockOut;

        if (result.Status == AttendanceStatus.DayOff)
        {
            Facts(story, clockIn, clockOut, single);
            story.Add(AttendanceExplanationStepKind.Note, null, "整天放假，不判定遲到、早退與異常；有刷卡照樣列出。", Titles.DayType);
            return "放假。";
        }
        if (result.Reason == AttendanceReason.DayTypeUnknown || rule is null)
        {
            Facts(story, clockIn, clockOut, single);
            story.Add(AttendanceExplanationStepKind.Note, null, "假日行事曆沒有這一天，或公文系統連不上：不知道這天要不要上班，所以不判定。", Titles.DayType, Titles.Undetermined);
            return "無法判定，不算異常。";
        }
        if (result.Reason == AttendanceReason.FormsUnavailable)
        {
            story.Add(AttendanceExplanationStepKind.Note, null, "沒有刷卡，表單又無法取得（公文系統連不上或對不到公文帳號）：不知道有沒有請假，不判異常。", Titles.Undetermined);
            return "無法判定，不算異常。";
        }

        var shift = new AttendanceEvaluator.Shift(day.Date, rule);
        var judgedIn = AttendanceEvaluator.Judged(result.ClockIn, options.PunchPrecision);
        var late = judgedIn > shift.FlexEnd;

        if (result.Reason == AttendanceReason.DayInProgress)
        {
            if (clockIn is { } arrived) ClockIn(story, arrived, judgedIn!.Value, late, rule, shift, today: true);
            if (Clock(result.ExpectedClockOut) is { } expected)
                story.Add(AttendanceExplanationStepKind.Rule, expected, late ? "應下班照表定下班。" : $"依上班時間算出的應下班（加 {Hours(rule.WorkHours)} 小時，含午休）。", Titles.Flex);
            if (Clock(result.AdjustedClockOut) is { } adjusted)
                story.Add(AttendanceExplanationStepKind.Form, adjusted, $"扣掉核准的表單 {Duration(result.FormSeconds)}，這時就可以下班。", Titles.Coverage);
            story.Add(AttendanceExplanationStepKind.Note, null, "今天還沒結束：下班刷卡晚上才更新，先不判定遲到、早退與異常。", Titles.Undetermined);
            return "今日未結算。";
        }

        if (clockIn is null)
            story.Add(AttendanceExplanationStepKind.Gap, null, "整天沒有刷卡。", Titles.NoPunch);
        else if (single)
            story.Add(AttendanceExplanationStepKind.Gap, clockIn, "只有這一筆刷卡，分不出是上班還是下班：判為異常，不算遲到或早退。", Titles.SinglePunch);
        else
        {
            ClockIn(story, clockIn.Value, judgedIn!.Value, late, rule, shift, today: false);
            if (Clock(result.ExpectedClockOut) is { } expected)
                story.Add(AttendanceExplanationStepKind.Rule, expected, late
                    ? "遲到，應下班改為表定下班。"
                    : $"應下班：從 {Stamp(Clock(Max(judgedIn.Value, shift.FlexStart))!.Value)} 起加 {Hours(rule.WorkHours)} 小時（含午休）。", Titles.Flex);
            if (result.EarlyLeaveSeconds > 0)
                story.Add(AttendanceExplanationStepKind.Gap, clockOut, $"下班刷卡，比應下班早 {Duration(result.EarlyLeaveSeconds)}。", Titles.EarlyLeave);
            else
                story.Add(AttendanceExplanationStepKind.Punch, clockOut, "下班刷卡，不早於應下班。", Titles.Punches);
            if (options.PunchPrecision == PunchPrecision.Minute)
                story.Add(AttendanceExplanationStepKind.Note, null, "遲到、早退以整分計：刷卡的秒數先捨去再比。", Titles.EarlyLeave);
        }

        if (result.RequiredSeconds == 0) return "正常：沒有遲到，也沒有早退。";

        Shortfall(story, result, shift, noWork: clockIn is null || single);
        Forms(story, day, result);
        return Conclusion(result);
    }

    /// <summary>放假與日型未知時照樣列出刷卡，不判定。</summary>
    private static void Facts(Story story, TimeOnly? clockIn, TimeOnly? clockOut, bool single)
    {
        if (clockIn is null) return;
        if (single)
        {
            story.Add(AttendanceExplanationStepKind.Punch, clockIn, "刷卡。");
            return;
        }
        story.Add(AttendanceExplanationStepKind.Punch, clockIn, "上班刷卡。");
        if (clockOut is not null) story.Add(AttendanceExplanationStepKind.Punch, clockOut, "下班刷卡。");
    }

    /// <summary>上班刷卡落在彈性時段的哪裡；今天只說位置，不說遲到多久（今天不判定）。</summary>
    private static void ClockIn(Story story, TimeOnly clockIn, DateTimeOffset judged, bool late, WorkdayRule rule, AttendanceEvaluator.Shift shift, bool today)
    {
        if (late)
        {
            if (today) story.Add(AttendanceExplanationStepKind.Punch, clockIn, $"上班刷卡，晚於彈性終點 {Time(rule.FlexEnd)}；今天先不算遲到。", Titles.Late);
            else story.Add(AttendanceExplanationStepKind.Gap, clockIn, $"上班刷卡，晚於彈性終點 {Time(rule.FlexEnd)}，遲到 {Duration(Seconds(judged - shift.FlexEnd))}。", Titles.Late);
            return;
        }
        story.Add(AttendanceExplanationStepKind.Punch, clockIn, judged < shift.FlexStart
            ? $"上班刷卡，早於彈性起點 {Time(rule.FlexStart)}，以 {Time(rule.FlexStart)} 起算。"
            : $"上班刷卡，在彈性時段 {Time(rule.FlexStart)}–{Time(rule.FlexEnd)} 內，不算遲到。", Titles.Flex);
    }

    /// <summary>要多少表單：工時差多少就要多少，但遲到的部分至少要抵掉（<see cref="AttendanceEvaluation.RequiredSeconds"/>）。</summary>
    private static void Shortfall(Story story, AttendanceEvaluation result, AttendanceEvaluator.Shift shift, bool noWork)
    {
        var (net, worked, required) = (shift.NetWorkSeconds, result.WorkedSeconds, result.RequiredSeconds);
        var text = noWork ? $"沒有上班時數，當天工時 {Duration(net)}都要有表單。"
            : required > net - worked ? $"實際上班 {Duration(worked)}（扣午休），當天工時 {Duration(net)}；遲到的部分（{Duration(required)}）至少要有表單抵掉，晚走不抵遲到。"
            : $"實際上班 {Duration(worked)}（扣午休），當天工時 {Duration(net)}，差 {Duration(required)}。";
        story.Add(AttendanceExplanationStepKind.Rule, null, text, Titles.Coverage);
    }

    /// <summary>表單抵掉多少：核准的才算補足，流程中的另外寫出來；表單無法取得時不當成沒請假。</summary>
    private static void Forms(Story story, AttendanceDay day, AttendanceEvaluation result)
    {
        if (day.Forms is null)
        {
            story.Add(AttendanceExplanationStepKind.Form, null, "表單無法取得：不知道有沒有請假，不當成未補足。", Titles.Undetermined);
            return;
        }
        if (day.Forms.Count == 0) story.Add(AttendanceExplanationStepKind.Form, null, "當天沒有假單或出差單。", Titles.Forms);
        else if (result.FiledSeconds == 0) story.Add(AttendanceExplanationStepKind.Form, null, "當天的表單不在上班時段內，不抵工時。", Titles.Coverage);
        if (result.FormSeconds > 0) story.Add(AttendanceExplanationStepKind.Form, null, $"核准的表單抵掉 {Duration(result.FormSeconds)}。", Titles.Forms);
        if (result.FiledSeconds - result.FormSeconds is var pending and > 0)
            story.Add(AttendanceExplanationStepKind.Form, null, $"流程中的表單 {Duration(pending)}，核准後才算。", Titles.Forms);
        if (result.Status == AttendanceStatus.Present && Clock(result.AdjustedClockOut) is { } adjusted)
            story.Add(AttendanceExplanationStepKind.Form, adjusted, "扣掉核准的表單後，這時就可以下班。", Titles.Coverage);
    }

    private static string Conclusion(AttendanceEvaluation result)
    {
        if (result.Status == AttendanceStatus.OnLeave) return "請假：核准的表單抵掉整天。";
        var what = result.Status == AttendanceStatus.Anomaly
            ? result.Reason == AttendanceReason.SinglePunch ? "缺一筆刷卡" : "未刷卡"
            : (result.LateSeconds > 0, result.EarlyLeaveSeconds > 0) switch { (true, true) => "遲到又早退", (true, false) => "遲到", _ => "早退" };
        var coverage = result.Coverage switch
        {
            GapCoverage.Covered => "核准的表單已補足",
            GapCoverage.Pending => $"核准的表單還差 {Duration(result.ShortSeconds)}，流程中的單核准後就補足",
            GapCoverage.Unexplained => $"核准的表單還差 {Duration(result.ShortSeconds)}，未補足",
            _ => "表單無法取得，不知道有沒有補足"
        };
        return $"{what}：{coverage}。";
    }

    private static TimeOnly? Clock(DateTimeOffset? value) => value is { } x ? TimeOnly.FromTimeSpan(x.ToTaipei().TimeOfDay) : null;

    /// <summary>整分寫「08:00」，有秒數才寫「08:31:19」。</summary>
    private static string Stamp(TimeOnly time) => time.Second == 0 ? Time(time) : time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a >= b ? a : b;

    private static int Seconds(TimeSpan span) => (int)Math.Floor(span.TotalSeconds);

    /// <summary>依序累積的步驟與引用的條文（不重複、依第一次出現的順序）。</summary>
    private sealed class Story
    {
        private readonly List<string> rules = [];

        public List<AttendanceExplanationStep> Steps { get; } = [];

        public IEnumerable<string> Rules => rules;

        public void Add(AttendanceExplanationStepKind kind, TimeOnly? at, string text, params string[] cites)
        {
            Steps.Add(new(kind, at, text));
            foreach (var title in cites)
                if (!rules.Contains(title)) rules.Add(title);
        }
    }
}
