namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 一個出勤日的判定結果。<paramref name="ClockIn"/>／<paramref name="ClockOut"/> 是最早與最晚刷卡（+08:00，精確到秒），任何狀態都照實回傳；
/// <paramref name="ExpectedClockOut"/> 與遲到、早退秒數只在 <see cref="AttendanceStatus.Present"/> 時有值，其餘為 null 與 0。
/// 遲到、早退用整數秒而不是 <see cref="TimeSpan"/>：JSON 是單純數字，前端直接格式化成「分:秒」或「時:分」，不必解析 <c>"00:05:00"</c> 這類字串。
/// <paramref name="Leave"/> 原樣帶回輸入，讓畫面並列「當天有假單」或「假單無法取得」。
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
    LeaveState Leave);

/// <summary>一個出勤日判定後的狀態；遲到與早退另以秒數表示，可與 <see cref="Present"/> 並存。</summary>
public enum AttendanceStatus
{
    /// <summary>有上下班刷卡，已判定遲到與早退（秒數為 0 表示沒有）。</summary>
    Present = 0,

    /// <summary>沒有刷卡，但有假單或出差單。</summary>
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
    /// <summary>異常：沒有刷卡，也沒有假單或出差單。</summary>
    NoPunchOrLeave = 0,

    /// <summary>無法判定：沒有刷卡，假單無法取得。</summary>
    LeaveUnavailable = 1,

    /// <summary>無法判定：不知道當天的日型（行事曆沒有這天或 GD 連不上）。</summary>
    DayTypeUnknown = 2,

    /// <summary>異常：只有一筆刷卡（或兩筆在同一秒），少了上班或下班；分不出缺哪一次，所以不算遲到早退。</summary>
    SinglePunch = 3,

    /// <summary>無法判定：今天還沒結束，打卡檔多半還沒匯入（打卡機 23:52 才輸出當天），沒刷卡或少一筆都還不算異常。</summary>
    DayInProgress = 4
}
