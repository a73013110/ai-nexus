using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Platform.Dashboard;

/// <summary>依檢視者時區劃分的每日時間序列區塊，供總覽圖表使用。</summary>
public static class DashboardDays
{
    /// <summary>
    /// 計算時間範圍內各曆日的資料筆數。
    /// SQL Server 在資料庫端執行分組計算；其餘資料庫提供者（如 SQLite 測試主機）則於記憶體中進行分組。
    /// </summary>
    public static async Task<IReadOnlyList<DashboardPointDto>> CountAsync(DbContext db, IQueryable<DateTimeOffset> times, DashboardScope scope, CancellationToken ct)
    {
        var window = times.Where(at => at >= scope.From && at < scope.Until);
        if (db.Database.IsSqlServer())
        {
            var rows = await window.GroupBy(at => at.AddMinutes(scope.OffsetMinutes).Date)
                .Select(g => new { Day = g.Key, Count = g.Count() }).OrderBy(x => x.Day).ToListAsync(ct);
            return [.. rows.Select(x => new DashboardPointDto(Day(x.Day), x.Count))];
        }
        var offset = TimeSpan.FromMinutes(scope.OffsetMinutes);
        return [.. (await window.ToListAsync(ct)).GroupBy(at => Day(at.ToOffset(offset).Date)).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new DashboardPointDto(g.Key, g.Count()))];
    }

    private static string Day(DateTime day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
