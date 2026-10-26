namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 出勤判定的輸入：一個人的一個出勤日。刷卡是當天最早與最晚的一筆（台灣本地時間；只有一筆時兩者相同或其中一個為 null），沒有刷卡時兩者都是 null。
/// <paramref name="Forms"/> 是與當天有交集的表單期間（原始起訖），null 表示表單無法取得（GD 連不上或對不到公文帳號），不能當成沒請假。
/// </summary>
public sealed record AttendanceDay(DateOnly Date, AttendanceDayType DayType, DateTimeOffset? FirstPunch, DateTimeOffset? LastPunch, IReadOnlyList<FormPeriod>? Forms)
{
    public FormState FormState => Forms is null ? FormState.Unavailable : Forms.Count > 0 ? FormState.Filed : FormState.None;
}

/// <summary>一張假單或出差單的一段期間；<paramref name="Approved"/> 為 false 是還在簽核，不算補足、只算「簽核中」。</summary>
public sealed record FormPeriod(DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool Approved);

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
public enum FormState
{
    None = 0,

    /// <summary>當天有假單或出差單（核准或簽核中）；是否補足見 <see cref="GapCoverage"/>。</summary>
    Filed = 1,

    /// <summary>表單無法取得：GD 連不上，或這個人對不到公文帳號（<c>GdFormLookup.Unmapped</c>）；不能當成沒請假。</summary>
    Unavailable = 2
}
