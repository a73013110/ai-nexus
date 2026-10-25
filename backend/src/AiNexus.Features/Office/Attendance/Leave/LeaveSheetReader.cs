using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.Data.External;

namespace AiNexus.Features.Office.Attendance.Leave;

/// <summary>
/// 從公文系統（GD）唯讀查詢請假單與出差單（ADR 0018）。GD 連不上不拋例外，回傳 <see cref="LeaveLookup.Unavailable"/>，讓出勤頁照常顯示刷卡結果。
/// </summary>
/// <remarks>
/// 只取流程狀態 Send、Decide、Finish 的單據（Temp、Back、Cancel 不算）。<c>Form_Leave.DayOfWeek</c> 不為空的假單，主單的起訖是整個範圍、
/// 不是實際請假的時段，改取 <c>Form_Leave_Group</c> 同單號的各列（舊系統做法），每一列一筆 <see cref="LeaveSheet"/>。
/// 期間與查詢區間相交才取出，起訖保留原值、不裁切到區間；結束剛好等於區間起點、或開始剛好等於區間終點的不算。
/// </remarks>
public sealed class LeaveSheetReader(GdAccounts accounts, ExternalDatabase<GdDatabase> gd)
{
    // SQL Server 比較字串時忽略尾端空白，所以只有空白的 DayOfWeek 也算空。排序留給呼叫端（分批查詢後本來就要重排）。
    private const string Sql = """
        SELECT l.[DesNo], l.[LeaveUserId] AS GdId, 0 AS Kind, COALESCE(k.[LeaveKind], l.[LeaveKind], '') AS Title,
               p.[StartsAt], p.[EndsAt], l.[FlowOver] AS FlowStatus
        FROM [dbo].[Form_Leave] l
        LEFT JOIN [dbo].[Form_LeaveKind] k ON k.[LeaveId] = l.[LeaveKind]
        CROSS APPLY (
            SELECT l.[LeaveSDate], l.[LeaveEDate] WHERE ISNULL(l.[DayOfWeek], '') = ''
            UNION ALL
            SELECT g.[LeaveSDate], g.[LeaveEDate] FROM [dbo].[Form_Leave_Group] g WHERE g.[DesNo] = l.[DesNo] AND l.[DayOfWeek] <> ''
        ) p ([StartsAt], [EndsAt])
        WHERE l.[LeaveUserId] IN @GdIds AND l.[FlowOver] IN ('Send', 'Decide', 'Finish')
          AND p.[StartsAt] < @RangeEnd AND p.[EndsAt] > @RangeStart
        UNION ALL
        SELECT t.[DesNo], t.[ChargeName], 1, COALESCE(t.[TravelMain], ''), t.[TravelSDate], t.[TravelEDate], t.[FlowOver]
        FROM [dbo].[Form_Travel] t
        WHERE t.[ChargeName] IN @GdIds AND t.[FlowOver] IN ('Send', 'Decide', 'Finish')
          AND t.[TravelSDate] < @RangeEnd AND t.[TravelEDate] > @RangeStart
        """;

    /// <summary>查詢 <paramref name="userIds"/> 在 <paramref name="from"/>～<paramref name="to"/>（出勤日，含頭尾）內的請假單與出差單。</summary>
    public async Task<LeaveLookup> ReadAsync(IReadOnlyCollection<Guid> userIds, DateOnly from, DateOnly to, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(to, from);
        if (await accounts.ResolveAsync(userIds, ct) is not { } map) return LeaveLookup.Unavailable;

        var usersByGdId = map.GdIds.SelectMany(x => x.Value, (x, gdId) => (gdId, user: x.Key)).ToLookup(x => x.gdId, x => x.user, StringComparer.OrdinalIgnoreCase);
        var (rangeStart, rangeEnd) = (from.ToDateTime(TimeOnly.MinValue), to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var rows = usersByGdId.Count == 0 ? [] : await gd.QueryInAsync<SheetRow>(Sql, usersByGdId.Select(x => x.Key),
            batch => new { GdIds = batch, RangeStart = rangeStart, RangeEnd = rangeEnd }, ct);
        if (rows is null) return LeaveLookup.Unavailable;

        var sheets = new Dictionary<Guid, List<LeaveSheet>>();
        foreach (var row in rows)
        {
            var sheet = new LeaveSheet(row.DesNo, row.Kind, row.Title, TaipeiTime.At(row.StartsAt), TaipeiTime.At(row.EndsAt), row.FlowStatus.Trim());
            foreach (var user in usersByGdId[row.GdId.Trim()])
            {
                if (!sheets.TryGetValue(user, out var list)) sheets[user] = list = [];
                list.Add(sheet);
            }
        }
        return new(true,
            sheets.ToDictionary(x => x.Key, x => (IReadOnlyList<LeaveSheet>)[.. x.Value.OrderBy(s => s.StartsAt).ThenBy(s => s.DesNo, StringComparer.Ordinal)]),
            map.Unmapped);
    }

    private sealed class SheetRow
    {
        public string DesNo { get; init; } = "";
        public string GdId { get; init; } = "";
        public LeaveSheetKind Kind { get; init; }
        public string Title { get; init; } = "";
        public DateTime StartsAt { get; init; }
        public DateTime EndsAt { get; init; }
        public string FlowStatus { get; init; } = "";
    }
}
