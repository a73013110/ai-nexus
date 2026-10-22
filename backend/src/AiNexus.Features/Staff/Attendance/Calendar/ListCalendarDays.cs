using AiNexus.Features.Staff.Attendance.Gd;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Staff.Attendance.Calendar;

/// <summary>
/// 假日設定頁：列出 <c>from</c>～<c>to</c>（都含）在 GD <c>dbo.Pub_Days</c> 的日期，依日期排序；最多 <see cref="CalendarDates.MaxRangeDays"/> 天。
/// GD 沒有列的日期不會出現（不補空白日），因為回寫只能改既有日期。走唯讀的 <see cref="GdAccess"/>；GD 不可用時回 <c>attendance_gd_unavailable</c>，不回空清單。
/// </summary>
internal sealed class ListCalendarDays(GdAccess gd)
{
    // hDate 是 datetime，以整天的半開區間比對，帶了時間的列也算在當天。
    private const string Sql = """
        SELECT [hDate] AS [Day], [AM] AS MorningOff, [PM] AS AfternoonOff, [hReMark] AS Remark
        FROM [dbo].[Pub_Days] WHERE [hDate] >= @From AND [hDate] < @Until ORDER BY [hDate]
        """;

    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (DateOnly? from, DateOnly? to, ListCalendarDays handler, CancellationToken ct) => handler.HandleAsync(from, to, ct).ToHttpResultAsync())
        .WithName("ListAttendanceCalendarDays");

    public async Task<Result<IReadOnlyList<CalendarDayDto>>> HandleAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (from is not { } start || to is not { } end || end < start || !CalendarDates.IsSupported(start) || !CalendarDates.IsSupported(end)
            || end.DayNumber - start.DayNumber >= CalendarDates.MaxRangeDays)
            return AttendanceErrors.CalendarRangeInvalid;
        var rows = await gd.QueryAsync<Row>(Sql, new { From = start.ToDateTime(TimeOnly.MinValue), Until = end.AddDays(1).ToDateTime(TimeOnly.MinValue) }, ct);
        if (rows is null) return AttendanceErrors.GdUnavailable;
        return Result<IReadOnlyList<CalendarDayDto>>.Ok([.. rows.Select(x => new CalendarDayDto(DateOnly.FromDateTime(x.Day), x.MorningOff, x.AfternoonOff, x.Remark))]);
    }

    private sealed class Row
    {
        public DateTime Day { get; init; }
        public bool MorningOff { get; init; }
        public bool AfternoonOff { get; init; }
        public string? Remark { get; init; }
    }
}
