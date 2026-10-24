using AiNexus.Platform.Errors;

namespace AiNexus.Features.Office.Attendance.Calendar;

/// <summary>
/// 假日設定頁：列出 <c>from</c>～<c>to</c>（都含）在 GD <c>dbo.Pub_Days</c> 的日期，依日期排序；最多 <see cref="CalendarDates.MaxRangeDays"/> 天。
/// GD 沒有列的日期不會出現（不補空白日），因為回寫只能改既有日期。GD 不可用時回 <c>attendance_gd_unavailable</c>，不回空清單。
/// </summary>
internal sealed class ListCalendarDays(CalendarReader calendar)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (DateOnly? from, DateOnly? to, ListCalendarDays handler, CancellationToken ct) => handler.HandleAsync(from, to, ct).ToHttpResultAsync())
        .WithName("ListAttendanceCalendarDays");

    public async Task<Result<IReadOnlyList<CalendarDayDto>>> HandleAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (from is not { } start || to is not { } end || end < start || !CalendarDates.IsSupported(start) || !CalendarDates.IsSupported(end)
            || end.DayNumber - start.DayNumber >= CalendarDates.MaxRangeDays)
            return AttendanceErrors.CalendarRangeInvalid;
        if (await calendar.ReadAsync(start, end, ct) is not { } days) return AttendanceErrors.GdUnavailable;
        return Result<IReadOnlyList<CalendarDayDto>>.Ok(days);
    }
}
