using AiNexus.Features.Admin.Integrations.Gd;

namespace AiNexus.Features.Staff.Attendance.Calendar;

/// <summary>
/// 讀 GD 假日行事曆（<c>dbo.Pub_Days</c>）：假日設定頁與出勤紀錄共用，走唯讀的 <see cref="GdAccess"/>。
/// GD 不可用時回傳 <see langword="null"/>（與「沒有列」的空清單不同），由呼叫端決定拒絕（假日設定）還是降級（出勤紀錄）。
/// </summary>
internal sealed class CalendarReader(GdAccess gd)
{
    // hDate 是 datetime，以整天的半開區間比對，帶了時間的列也算在當天。
    private const string Sql = """
        SELECT [hDate] AS [Day], [AM] AS MorningOff, [PM] AS AfternoonOff, [hReMark] AS Remark
        FROM [dbo].[Pub_Days] WHERE [hDate] >= @From AND [hDate] < @Until ORDER BY [hDate]
        """;

    /// <summary><paramref name="from"/>～<paramref name="to"/>（都含）在 GD 有列的日期，依日期排序；日期須通過 <see cref="CalendarDates.IsSupported"/>。</summary>
    public async Task<IReadOnlyList<CalendarDayDto>?> ReadAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await gd.QueryAsync<Row>(Sql, new { From = from.ToDateTime(TimeOnly.MinValue), Until = to.AddDays(1).ToDateTime(TimeOnly.MinValue) }, ct);
        return rows?.Select(x => new CalendarDayDto(DateOnly.FromDateTime(x.Day), x.MorningOff, x.AfternoonOff, x.Remark)).ToList();
    }

    private sealed class Row
    {
        public DateTime Day { get; init; }
        public bool MorningOff { get; init; }
        public bool AfternoonOff { get; init; }
        public string? Remark { get; init; }
    }
}
