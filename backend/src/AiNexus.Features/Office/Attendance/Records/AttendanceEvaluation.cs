namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 一個出勤日的判定結果。<paramref name="ClockIn"/>／<paramref name="ClockOut"/> 是最早與最晚刷卡（+08:00，精確到秒），任何狀態都照實回傳。
/// <paramref name="ExpectedClockOut"/> 與遲到、早退秒數是不看表單的原始判定（同舊系統），只在 <see cref="AttendanceStatus.Present"/> 時有值，其餘為 null 與 0。
/// 遲到、早退用整數秒而不是 <see cref="TimeSpan"/>：JSON 是單純數字，前端直接格式化成「分:秒」或「時:分」，不必解析 <c>"00:05:00"</c> 這類字串。
/// 表單的部分（規則見 <see cref="AttendanceEvaluator"/>）：
/// <list type="bullet">
/// <item><paramref name="FormState"/>：當天有沒有表單，讓畫面並列「有假單」或「表單無法取得」。</item>
/// <item><paramref name="FormSeconds"/>：核准表單抵掉的秒數（截到當天上班時段、扣午休、重疊只算一次）。</item>
/// <item><paramref name="AdjustedClockOut"/>：扣掉核准表單後的應下班；表單還不夠抵遲到、或已抵掉整天時為 null。</item>
/// <item><paramref name="Coverage"/>：有遲到、早退或異常時，核准的表單是否補足，沒有缺口與今天為 null；
/// <paramref name="ShortSeconds"/> 是核准表單還差的秒數（未補足與簽核中才有）。</item>
/// <item><see cref="Gaps"/>：缺口落在哪些時段，讓 HR 對照簽核中假單的起訖、或通知補哪一段的單。</item>
/// </list>
/// </summary>
public sealed record AttendanceEvaluation(
    DateOnly Date,
    AttendanceDayType DayType,
    AttendanceStatus Status,
    AttendanceReason? Reason,
    DateTimeOffset? ClockIn,
    DateTimeOffset? ClockOut,
    DateTimeOffset? ExpectedClockOut,
    int LateSeconds,
    int EarlyLeaveSeconds,
    FormState FormState,
    int FormSeconds = 0,
    DateTimeOffset? AdjustedClockOut = null,
    GapCoverage? Coverage = null,
    int ShortSeconds = 0)
{
    /// <summary>
    /// 缺口的時段（原始的遲到、早退與沒刷卡，不扣表單）；沒有缺口、只有一筆刷卡（分不出缺哪一段）與今天為空。
    /// 只是參考：補足比的是時數不是區間（見 <see cref="AttendanceEvaluator"/>），表單不必蓋在這些時段上，差多少以 <see cref="ShortSeconds"/> 為準。
    /// </summary>
    public IReadOnlyList<AttendanceGap> Gaps { get; init; } = [];
}

/// <summary>一段缺口：<paramref name="StartsAt"/>～<paramref name="EndsAt"/>（+08:00）。</summary>
public sealed record AttendanceGap(AttendanceGapKind Kind, DateTimeOffset StartsAt, DateTimeOffset EndsAt);

public enum AttendanceGapKind
{
    /// <summary>彈性終點到上班刷卡。</summary>
    Late = 0,

    /// <summary>下班刷卡到應下班。</summary>
    EarlyLeave = 1,

    /// <summary>整天沒刷卡：表定上班到表定下班。</summary>
    Absent = 2
}

/// <summary>
/// 當天的缺口（遲到、早退或異常）有沒有表單補足（規則見 <see cref="AttendanceEvaluator"/>）。只有核准的表單算數；
/// 要靠簽核中的單才補得足是 <see cref="Pending"/>，讓人知道還在等簽核，不是沒請假。
/// </summary>
public enum GapCoverage
{
    /// <summary>未補足：核准加簽核中的表單都不夠。</summary>
    Unexplained = 0,

    /// <summary>簽核中：核准的不夠，加上簽核中的才夠。</summary>
    Pending = 1,

    /// <summary>已補足：核准的表單時數夠。</summary>
    Covered = 2,

    /// <summary>表單無法取得（GD 連不上或對不到公文帳號）：不知道有沒有請假，不當成未補足。</summary>
    Unknown = 3
}

/// <summary>一個出勤日判定後的狀態；遲到與早退另以秒數表示，可與 <see cref="Present"/> 並存。</summary>
public enum AttendanceStatus
{
    /// <summary>有上下班刷卡，已判定遲到與早退（秒數為 0 表示沒有）。</summary>
    Present = 0,

    /// <summary>沒有刷卡，核准的假單或出差單補足當天的工時。</summary>
    OnLeave = 1,

    /// <summary>異常，原因見 <see cref="AttendanceEvaluation.Reason"/>。</summary>
    Anomaly = 2,

    /// <summary>資料不足以判定，原因見 <see cref="AttendanceEvaluation.Reason"/>；寧鬆勿嚴，不當成異常。</summary>
    Undetermined = 3,

    /// <summary>整天放假：不判定遲到、早退與異常，刷卡照樣回傳。</summary>
    DayOff = 4
}

/// <summary><see cref="AttendanceStatus.Anomaly"/> 與 <see cref="AttendanceStatus.Undetermined"/> 的原因。</summary>
public enum AttendanceReason
{
    /// <summary>異常：沒有刷卡，核准的表單也沒補足當天的工時（沒有表單、只請部分時段或還在簽核，見 <see cref="AttendanceEvaluation.Coverage"/>）。</summary>
    NoPunchOrLeave = 0,

    /// <summary>無法判定：沒有刷卡，表單無法取得。</summary>
    FormsUnavailable = 1,

    /// <summary>無法判定：不知道當天的日型（行事曆沒有這天或 GD 連不上）。</summary>
    DayTypeUnknown = 2,

    /// <summary>異常：只有一筆刷卡（或兩筆在同一秒），少了上班或下班；分不出缺哪一次，所以不算遲到早退。</summary>
    SinglePunch = 3,

    /// <summary>無法判定：今天還沒結束，打卡檔多半還沒匯入（打卡機 23:52 才輸出當天），沒刷卡或少一筆都還不算異常。</summary>
    DayInProgress = 4
}
