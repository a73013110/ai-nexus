using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Staff.Attendance.Gd;

/// <summary>
/// 回寫 GD 假日（<c>dbo.Pub_Days</c>）專用的可寫入連線，設定位於 <c>Databases:GdCalendar</c>（ADR 0018）。
/// 與唯讀的 <see cref="Admin.Integrations.Sources.GdDatabase"/> 分開：那個帳號維持唯讀、啟動時強制檢查；
/// 這裡用另一個只對 <c>Pub_Days</c> 有 SELECT 與 <c>UPDATE (AM, PM, hReMark, UserId)</c> 的最小權限帳號。未設定時只停用回寫，不影響啟動。
/// </summary>
public sealed class GdCalendarDatabase : ISqlDatabaseDefinition
{
    private GdCalendarDatabase() { }

    public static string Name => "GdCalendar";

    /// <summary>明確寫出 false：這是全系統唯一允許寫入 GD 的連線，不是漏設。</summary>
    public static bool ReadOnly => false;
}
