namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 出勤判定的輸入：一個人的一個出勤日。刷卡是當天最早與最晚的一筆（台灣本地時間；只有一筆時兩者相同或其中一個為 null），沒有刷卡時兩者都是 null。
/// </summary>
public sealed record AttendanceDay(DateOnly Date, AttendanceDayType DayType, DateTimeOffset? FirstPunch, DateTimeOffset? LastPunch, LeaveState Leave);

/// <summary>當天的日型，由 GD <c>Pub_Days</c> 的 AM、PM 推得（<see cref="AttendanceEvaluator.DayTypeOf"/>）。</summary>
public enum AttendanceDayType
{
    FullDay = 0,
    MorningOff = 1,
    AfternoonOff = 2,
    DayOff = 3,

    /// <summary>行事曆沒有這天（平日）或 GD 連不上：不知道該用哪一套規則，不判定。</summary>
    Unknown = 4
}

/// <summary>當天的假單與出差單。</summary>
public enum LeaveState
{
    None = 0,

    /// <summary>當天有假單或出差單（只並列，不比對時數是否補足遲到早退）。</summary>
    Filed = 1,

    /// <summary>假單無法取得：GD 連不上，或這個人對不到公文帳號（<c>LeaveLookup.Unmapped</c>）；不能當成沒請假。</summary>
    Unavailable = 2
}
