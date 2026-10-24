using AiNexus.Data.App.Attendance;
using AiNexus.Features.Office.Attendance.Calendar;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>出勤紀錄的查詢條件，查詢與匯出共用；分頁只屬於查詢。</summary>
public sealed class AttendanceRecordsFilter
{
    /// <summary>一年（與假日行事曆一次讀取的上限相同）：匯出整年或單人整年的需求；畫面依人分頁，區間長不會讓單一回應過大。</summary>
    public const int MaxRangeDays = CalendarDates.MaxRangeDays;

    /// <summary>出勤日（含），必填。</summary>
    public DateOnly? From { get; set; }

    /// <summary>出勤日（含），必填。</summary>
    public DateOnly? To { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>只有姓名、還沒綁帳號的持有人（持有期間的 <c>HolderName</c>）；只有全公司範圍查得到。</summary>
    public string? Holder { get; set; }

    /// <summary>該單位及下層的目前成員。</summary>
    public Guid? UnitId { get; set; }

    /// <summary>含 HR 標為不列入出勤的人；只影響全公司範圍。</summary>
    public bool? IncludeExcluded { get; set; }

    /// <summary>含沒有刷卡、也沒有假單的放假日；有刷卡或假單的放假日一律列出。</summary>
    public bool? IncludeDaysOff { get; set; }

    /// <summary>起訖必填、結束不早於開始、最長 <see cref="MaxRangeDays"/> 天，而且在 GD 行事曆支援的年份內；不合法時為 null。</summary>
    internal (DateOnly Start, DateOnly End)? Range()
        => From is { } start && To is { } end && end >= start && CalendarDates.IsSupported(start) && CalendarDates.IsSupported(end)
            && end.DayNumber - start.DayNumber < MaxRangeDays ? (start, end) : null;

    /// <summary>去掉前後空白的姓名；空白視同沒有指定。</summary>
    internal string? HolderName => string.IsNullOrWhiteSpace(Holder) ? null : Holder.Trim();

    internal bool IsValid() => HolderName is not { Length: > CardAssignment.HolderNameMaxLength };
}
