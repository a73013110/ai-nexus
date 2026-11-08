using System.Diagnostics.CodeAnalysis;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.Data.External;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Time;
using AiNexus.Platform.Validation;

namespace AiNexus.Features.Office.CalendarSync.Sync;

/// <summary>GD 上一段應出現在日曆的請假或出差期間；時間是 +08:00。</summary>
public sealed record AbsenceSegment(string DesNo, string SegmentKey, string Kind, string ApplicantName, string? AgentName, string FlowStatus, DateTimeOffset StartsAt, DateTimeOffset EndsAt)
{
    public (string DesNo, string SegmentKey) Key => (DesNo, SegmentKey);
}

/// <summary>
/// 從公文系統（GD）唯讀查詢應出現在日曆的請假單與出差單：流程中（Send）與核准（Decide），與出勤判定一致；
/// 退回結案（Finish）不是核准，舊系統把它同步上去是錯的（見 <see cref="GdFormFlowOver.Finish"/>）。
/// 跨區間假單（<c>Form_Leave.DayOfWeek</c> 不為空）改取 <c>Form_Leave_Group</c> 的各段，段的鍵沿用舊系統的「開始日期 yyyyMMdd＋DayOfWeek」。
/// 請假的人取 <c>LeaveUserId</c>（舊系統用 <c>ChargeName</c>，代人填單時會寫成填單的人）。
/// 起訖時間缺漏或不合理（結束不晚於開始）的段略過：日曆放不了，也不是同步能修正的問題。
/// </summary>
[Scoped]
public sealed class GdAbsenceReader(ExternalDatabase<GdDatabase> gd)
{
    // /*filter*/ 換成篩選條件：期間窗（結束時間不早於窗的起點）或指定文號。兩個 SELECT 的欄位順序必須一致。
    private const string Template = $$"""
        SELECT s.[DesNo], s.[SegmentKey], s.[Kind], COALESCE(u.[NAME], s.[PersonId]) AS ApplicantName, a.[NAME] AS AgentName, s.[FlowStatus], s.[StartsAt], s.[EndsAt]
        FROM (
            SELECT l.[DesNo], p.[SegmentKey], '{{CalendarEventKinds.Leave}}' AS Kind, l.[LeaveUserId] AS PersonId, l.[AgentUserId] AS AgentId, l.[FlowOver] AS FlowStatus, p.[StartsAt], p.[EndsAt]
            FROM [dbo].[Form_Leave] l
            CROSS APPLY (
                SELECT '', l.[LeaveSDate], l.[LeaveEDate] WHERE ISNULL(l.[DayOfWeek], '') = ''
                UNION ALL
                SELECT CONVERT(varchar(8), g.[LeaveSDate], 112) + ISNULL(g.[DayOfWeek], ''), g.[LeaveSDate], g.[LeaveEDate]
                FROM [dbo].[Form_Leave_Group] g WHERE g.[DesNo] = l.[DesNo] AND l.[DayOfWeek] <> ''
            ) p ([SegmentKey], [StartsAt], [EndsAt])
            WHERE l.[FlowOver] IN ('{{nameof(GdFormFlowOver.Send)}}', '{{nameof(GdFormFlowOver.Decide)}}')
            UNION ALL
            SELECT t.[DesNo], '', '{{CalendarEventKinds.Travel}}', t.[ChargeName], t.[AgentUserId], t.[FlowOver], t.[TravelSDate], t.[TravelEDate]
            FROM [dbo].[Form_Travel] t
            WHERE t.[FlowOver] IN ('{{nameof(GdFormFlowOver.Send)}}', '{{nameof(GdFormFlowOver.Decide)}}')
        ) s
        LEFT JOIN [dbo].[Sys_User] u ON u.[ID] = s.[PersonId]
        LEFT JOIN [dbo].[Sys_User] a ON a.[ID] = s.[AgentId]
        WHERE s.[StartsAt] IS NOT NULL AND s.[EndsAt] > s.[StartsAt] AND (/*filter*/)
        ORDER BY s.[DesNo], s.[SegmentKey], s.[StartsAt], s.[EndsAt]
        """;

    private static readonly string WindowSql = Template.Replace("/*filter*/", "s.[EndsAt] >= @WindowStart", StringComparison.Ordinal);
    private static readonly string ByDesNoSql = Template.Replace("/*filter*/", "s.[DesNo] IN @DesNos", StringComparison.Ordinal);

    // 不限流程狀態：要知道的正是單為什麼不再同步（退回、銷單、退回結案）。
    private const string FlowSql = """
        SELECT [DesNo], [FlowOver] AS FlowStatus FROM [dbo].[Form_Leave] WHERE [DesNo] IN @DesNos
        UNION ALL
        SELECT [DesNo], [FlowOver] FROM [dbo].[Form_Travel] WHERE [DesNo] IN @DesNos
        """;

    /// <summary>結束時間不早於 <paramref name="windowStart"/> 的期間；GD 不可用時回 null（與空清單不同，呼叫端不可據此移除任何事件）。</summary>
    public async Task<IReadOnlyList<AbsenceSegment>?> ReadWindowAsync(DateTimeOffset windowStart, CancellationToken ct)
    {
        var rows = await gd.QueryAsync<Row>(WindowSql, new { WindowStart = windowStart.ToTaipei().DateTime }, ct);
        return rows is null ? null : Map(rows);
    }

    /// <summary>指定文號的期間（不論時間），用來確認窗內的事件是單被撤回，還是改成窗外的日期；GD 不可用時回 null。</summary>
    public async Task<IReadOnlyList<AbsenceSegment>?> ReadByDesNoAsync(IReadOnlyCollection<string> desNos, CancellationToken ct)
    {
        if (desNos.Count == 0) return [];
        var rows = await gd.QueryInAsync<Row>(ByDesNoSql, desNos, batch => new { DesNos = batch }, ct);
        return rows is null ? null : Map(rows);
    }

    /// <summary>指定文號目前的流程狀態（文號 → FlowOver），查無此單的文號不在結果裡；GD 不可用時回 null。</summary>
    public async Task<IReadOnlyDictionary<string, string>?> ReadFlowStatusesAsync(IReadOnlyCollection<string> desNos, CancellationToken ct)
    {
        var rows = await gd.QueryInAsync<FlowRow>(FlowSql, desNos, batch => new { DesNos = batch }, ct);
        return rows?.DistinctBy(x => x.DesNo.Trim(), StringComparer.Ordinal).ToDictionary(x => x.DesNo.Trim(), x => x.FlowStatus.Trim(), StringComparer.Ordinal);
    }

    private static List<AbsenceSegment> Map(IEnumerable<Row> rows)
    {
        var segments = new List<AbsenceSegment>();
        var keys = new HashSet<(string, string)>();
        foreach (var row in rows)
        {
            var desNo = row.DesNo.Trim();
            var key = row.SegmentKey.Trim();
            // 同一張單兩段同一天開始、DayOfWeek 也相同時鍵會重複；加上序號讓每段各自一個事件，不讓唯一索引在寫入時失敗。
            // 序號依查詢的 ORDER BY（時間先後）決定，每輪都一樣，否則兩段會輪流換鍵、每輪各更新一次。
            for (var n = 2; !keys.Add((desNo, key)); n++) key = $"{row.SegmentKey.Trim()}#{n}";
            segments.Add(new(desNo, key, row.Kind, Clip(row.ApplicantName.TrimToNull() ?? desNo), Clip(row.AgentName.TrimToNull()), row.FlowStatus.Trim(),
                TaipeiTime.At(row.StartsAt), TaipeiTime.At(row.EndsAt)));
        }
        return segments;
    }

    [return: NotNullIfNotNull(nameof(text))]
    private static string? Clip(string? text) => text is null || text.Length <= CalendarEvent.NameMaxLength ? text : text[..CalendarEvent.NameMaxLength];

    private sealed class FlowRow
    {
        public string DesNo { get; init; } = "";
        public string FlowStatus { get; init; } = "";
    }

    private sealed class Row
    {
        public string DesNo { get; init; } = "";
        public string SegmentKey { get; init; } = "";
        public string Kind { get; init; } = "";
        public string? ApplicantName { get; init; }
        public string? AgentName { get; init; }
        public string FlowStatus { get; init; } = "";
        public DateTime StartsAt { get; init; }
        public DateTime EndsAt { get; init; }
    }
}
