using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Platform.Dashboard;

/// <summary>依檢視者時區劃分的每日時間序列區塊，供總覽圖表使用。</summary>
public static class DashboardDays
{
    /// <summary>
    /// 計算時間範圍內各曆日的資料筆數，分組在資料庫端完成。
    /// </summary>
    public static async Task<IReadOnlyList<DashboardPointDto>> CountAsync(IQueryable<DateTimeOffset> times, DashboardScope scope, CancellationToken ct)
    {
        var rows = await times.Where(at => at >= scope.From && at < scope.Until)
            .GroupBy(at => at.AddMinutes(scope.OffsetMinutes).Date)
            .Select(g => new { Day = g.Key, Count = g.Count() }).OrderBy(x => x.Day).ToListAsync(ct);
        return [.. rows.Select(x => new DashboardPointDto(Day(x.Day), x.Count))];
    }

    private static string Day(DateTime day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
