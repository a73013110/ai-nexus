using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Features.Platform.Gd;

namespace AiNexus.Features.Office.CalendarSync.Sync;

/// <summary>事件一個欄位的變更；<see cref="Field"/> 是事件的欄位名（camelCase），值是資料庫的原始值，顯示由前端決定。</summary>
public sealed record CalendarEventFieldChange(string Field, string? From, string? To);

/// <summary>
/// 把「這一列為什麼要推送」記在待推送的列上（純函式，不碰資料庫與 Google），推送時寫進當輪的明細。
/// 必須在列的內容被新值覆蓋之前呼叫，舊值只有這時候還在。
/// </summary>
public static class CalendarEventChanges
{
    // 中文不跳脫，欄位上限才算得出來（全部欄位都變也放得下 CalendarEvent.ChangesMaxLength）。
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// 內容變了或從移除救回。上次的變更還沒推送成功時併進來、保留最早的舊值：
    /// 日曆上還是那個舊值，明細要寫的是日曆實際的變化（A→B 沒推成、又改成 C，就是 A→C；改回 A 則不算變更）。
    /// </summary>
    public static void NoteUpdate(CalendarEvent row, AbsenceSegment segment)
    {
        var restored = row.RemovedAt is not null || row.State == CalendarEventStates.Removed;
        // 救回時，待推送的是移除的原因，與內容變更無關。
        var fields = restored ? [] : Parse(row.PendingChangesJson).ToDictionary(x => x.Field, StringComparer.Ordinal);
        foreach (var change in Compare(row, segment))
            fields[change.Field] = fields.TryGetValue(change.Field, out var earlier) ? change with { From = earlier.From } : change;
        row.PendingCause = restored ? CalendarEventCauses.Restored : CalendarEventCauses.Content;
        row.PendingChangesJson = Serialize([.. fields.Values.Where(x => x.From != x.To)]);
    }

    /// <summary>判定移除：依單據的現況（文號 → GD FlowOver）說明原因；<paramref name="flows"/> 為 null 表示查不到現況，不能猜成單據已不存在。</summary>
    public static void NoteRemoval(CalendarEvent row, IReadOnlyDictionary<string, string>? flows)
    {
        (row.PendingCause, row.PendingChangesJson) = flows switch
        {
            null => (CalendarEventCauses.Unknown, null),
            _ when !flows.TryGetValue(row.DesNo, out var flow) => (CalendarEventCauses.FormMissing, (string?)null),
            _ when flows[row.DesNo] is nameof(GdFormFlowOver.Send) or nameof(GdFormFlowOver.Decide) => (CalendarEventCauses.SegmentGone, null),
            // 舊系統轉入的列可能本來就是這個狀態，舊值相同就不寫，免得出現「退回結案 → 退回結案」。
            _ => (CalendarEventCauses.FormStatus, Serialize([new("flowStatus", row.FlowStatus == flows[row.DesNo] ? null : row.FlowStatus, flows[row.DesNo])])),
        };
    }

    public static IReadOnlyList<CalendarEventFieldChange> Parse(string? json)
        => json is null ? [] : JsonSerializer.Deserialize<List<CalendarEventFieldChange>>(json, Json) ?? [];

    private static string? Serialize(List<CalendarEventFieldChange> changes) => changes.Count == 0 ? null : JsonSerializer.Serialize(changes, Json);

    private static IEnumerable<CalendarEventFieldChange> Compare(CalendarEvent row, AbsenceSegment segment)
    {
        if (row.Kind != segment.Kind) yield return new("kind", row.Kind, segment.Kind);
        if (row.ApplicantName != segment.ApplicantName) yield return new("applicantName", row.ApplicantName, segment.ApplicantName);
        if (row.AgentName != segment.AgentName) yield return new("agentName", row.AgentName, segment.AgentName);
        if (row.FlowStatus != segment.FlowStatus) yield return new("flowStatus", row.FlowStatus, segment.FlowStatus);
        if (row.StartsAt != segment.StartsAt) yield return new("startsAt", Time(row.StartsAt), Time(segment.StartsAt));
        if (row.EndsAt != segment.EndsAt) yield return new("endsAt", Time(row.EndsAt), Time(segment.EndsAt));
    }

    private static string Time(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
}
