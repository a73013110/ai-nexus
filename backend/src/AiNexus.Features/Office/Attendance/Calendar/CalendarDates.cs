namespace AiNexus.Features.Office.Attendance.Calendar;

/// <summary>假日查詢與回寫共用的日期界線。</summary>
internal static class CalendarDates
{
    /// <summary>一次最多查一年（含閏年）：HR 通常看一個月，排年度行事曆時看整年；GD 是別人的系統，不開放整張表。</summary>
    public const int MaxRangeDays = 366;

    /// <summary>
    /// GD 的 <c>hDate</c> 是 datetime（1753 年起），超出範圍的日期代入參數會在 SqlClient 拋出非資料庫例外，
    /// 所以先擋在合理的年份內；這個範圍外的日期在 GD 不可能存在。
    /// </summary>
    public static bool IsSupported(DateOnly day) => day.Year is >= 1900 and <= 9998;
}
