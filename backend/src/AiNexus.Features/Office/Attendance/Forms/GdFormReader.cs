using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.Data.External;

namespace AiNexus.Features.Office.Attendance.Forms;

/// <summary>
/// 從公文系統（GD）唯讀查詢請假單與出差單（ADR 0018）。GD 連不上不拋例外，回傳 <see cref="GdFormLookup.Unavailable"/>，讓出勤頁照常顯示刷卡結果。
/// </summary>
/// <remarks>
/// 只取已核准（<see cref="GdFormFlowOver.Decide"/>）與流程中（<see cref="GdFormFlowOver.Send"/>）的表單：核准的才算補足，流程中的並列讓人知道還在等誰簽。
/// 暫存、退回、銷單與退回結案（<see cref="GdFormFlowOver.Finish"/>，GD 查證過是申請人把退回的單結案，不是核准）都不算。
/// <c>Form_Leave.DayOfWeek</c> 不為空的假單，主單的起訖是整個範圍、不是實際請假的時段，改取 <c>Form_Leave_Group</c> 同單號的各列（舊系統做法），
/// 每一列一筆 <see cref="GdForm"/>。期間與查詢區間相交才取出，起訖保留原值、不裁切到區間；結束剛好等於區間起點、或開始剛好等於區間終點的不算。
/// 流程中的單另從 <c>FLOW_CURRENT</c> 取目前關卡（<c>PERSON_ID</c> 是「部門_公文帳號」）與簽核人姓名；同一單同時停在多關時取最後一關。
/// </remarks>
public sealed class GdFormReader(GdAccounts accounts, ExternalDatabase<GdDatabase> gd)
{
    // SQL Server 比較字串時忽略尾端空白，所以只有空白的 DayOfWeek 也算空。排序留給呼叫端（分批查詢後本來就要重排）。
    private const string Sql = $$"""
        SELECT s.*, w.[Signer] AS CurrentSigner, w.[Step] AS CurrentStep
        FROM (
            SELECT l.[DesNo], l.[LeaveUserId] AS GdId, 0 AS Kind, COALESCE(k.[LeaveKind], l.[LeaveKind], '') AS Title,
                   p.[StartsAt], p.[EndsAt], l.[FlowOver] AS FlowStatus
            FROM [dbo].[Form_Leave] l
            LEFT JOIN [dbo].[Form_LeaveKind] k ON k.[LeaveId] = l.[LeaveKind]
            CROSS APPLY (
                SELECT l.[LeaveSDate], l.[LeaveEDate] WHERE ISNULL(l.[DayOfWeek], '') = ''
                UNION ALL
                SELECT g.[LeaveSDate], g.[LeaveEDate] FROM [dbo].[Form_Leave_Group] g WHERE g.[DesNo] = l.[DesNo] AND l.[DayOfWeek] <> ''
            ) p ([StartsAt], [EndsAt])
            WHERE l.[LeaveUserId] IN @GdIds AND l.[FlowOver] IN ('{{nameof(GdFormFlowOver.Send)}}', '{{nameof(GdFormFlowOver.Decide)}}')
              AND p.[StartsAt] < @RangeEnd AND p.[EndsAt] > @RangeStart
            UNION ALL
            SELECT t.[DesNo], t.[ChargeName], 1, COALESCE(t.[TravelMain], ''), t.[TravelSDate], t.[TravelEDate], t.[FlowOver]
            FROM [dbo].[Form_Travel] t
            WHERE t.[ChargeName] IN @GdIds AND t.[FlowOver] IN ('{{nameof(GdFormFlowOver.Send)}}', '{{nameof(GdFormFlowOver.Decide)}}')
              AND t.[TravelSDate] < @RangeEnd AND t.[TravelEDate] > @RangeStart
        ) s
        OUTER APPLY (
            SELECT TOP 1 u.[NAME], c.[STATUS]
            FROM [dbo].[FLOW_CURRENT] c
            LEFT JOIN [dbo].[Sys_User] u ON u.[ID] = SUBSTRING(c.[PERSON_ID], CHARINDEX('_', c.[PERSON_ID]) + 1, 50)
            WHERE c.[KEY_ID] = s.[DesNo] AND s.[FlowStatus] = '{{nameof(GdFormFlowOver.Send)}}'
            ORDER BY c.[FLOW_NO] DESC
        ) w ([Signer], [Step])
        """;

    /// <summary>查詢 <paramref name="userIds"/> 在 <paramref name="from"/>～<paramref name="to"/>（出勤日，含頭尾）內的請假單與出差單。</summary>
    public async Task<GdFormLookup> ReadAsync(IReadOnlyCollection<Guid> userIds, DateOnly from, DateOnly to, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(to, from);
        if (await accounts.ResolveAsync(userIds, ct) is not { } map) return GdFormLookup.Unavailable;

        var usersByGdId = map.GdIds.SelectMany(x => x.Value, (x, gdId) => (gdId, user: x.Key)).ToLookup(x => x.gdId, x => x.user, StringComparer.OrdinalIgnoreCase);
        var (rangeStart, rangeEnd) = (from.ToDateTime(TimeOnly.MinValue), to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var rows = usersByGdId.Count == 0 ? [] : await gd.QueryInAsync<FormRow>(Sql, usersByGdId.Select(x => x.Key),
            batch => new { GdIds = batch, RangeStart = rangeStart, RangeEnd = rangeEnd }, ct);
        if (rows is null) return GdFormLookup.Unavailable;

        var forms = new Dictionary<Guid, List<GdForm>>();
        foreach (var row in rows)
        {
            // SQL 已限定兩種狀態；解析不了（GD 改版出現新寫法）就略過，不當成核准。
            if (!GdFlowOver.TryParse(row.FlowStatus, out var flow)) continue;
            var form = new GdForm(row.DesNo, row.Kind, row.Title, TaipeiTime.At(row.StartsAt), TaipeiTime.At(row.EndsAt), flow,
                Text(row.CurrentSigner), Text(row.CurrentStep));
            foreach (var user in usersByGdId[row.GdId.Trim()])
            {
                if (!forms.TryGetValue(user, out var list)) forms[user] = list = [];
                list.Add(form);
            }
        }
        return new(true,
            forms.ToDictionary(x => x.Key, x => (IReadOnlyList<GdForm>)[.. x.Value.OrderBy(s => s.StartsAt).ThenBy(s => s.DesNo, StringComparer.Ordinal)]),
            map.Unmapped);
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class FormRow
    {
        public string DesNo { get; init; } = "";
        public string GdId { get; init; } = "";
        public GdFormKind Kind { get; init; }
        public string Title { get; init; } = "";
        public DateTime StartsAt { get; init; }
        public DateTime EndsAt { get; init; }
        public string FlowStatus { get; init; } = "";
        public string? CurrentSigner { get; init; }
        public string? CurrentStep { get; init; }
    }
}
