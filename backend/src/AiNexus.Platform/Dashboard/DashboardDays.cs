using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Platform.Dashboard;

/// <summary>Daily buckets in the viewer's time zone for overview series.</summary>
public static class DashboardDays
{
    /// <summary>
    /// Counts timestamps per local day inside the scope's window. SQL Server groups in the database; other providers
    /// (the SQLite test host) group the window's rows in memory.
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
