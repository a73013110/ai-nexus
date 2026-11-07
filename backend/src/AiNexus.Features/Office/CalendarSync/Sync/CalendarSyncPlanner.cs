using AiNexus.Data.App.CalendarSync;

namespace AiNexus.Features.Office.CalendarSync.Sync;

/// <summary>
/// 已記錄的一列，規劃只需要的部分。<paramref name="InWindow"/> 表示它還在日曆上（沒有移除）、結束時間在期間窗內，
/// 是「GD 沒有就移除」的候選；窗外的列只在 GD 又出現同一段時才會被更新或救回。
/// </summary>
public sealed record KnownEvent(Guid Id, string DesNo, string SegmentKey, byte[] ContentHash, string State, bool InWindow);

/// <summary>要改內容（或從移除救回）的既有列。</summary>
public sealed record EventChange(Guid Id, AbsenceSegment Segment, byte[] ContentHash);

/// <summary>新的一段期間。</summary>
public sealed record EventCreation(AbsenceSegment Segment, byte[] ContentHash);

/// <summary>一輪的規劃結果；<see cref="HeldRemovals"/> 大於 0 表示安全閥擋下了移除，<see cref="Removals"/> 為空。</summary>
public sealed record CalendarSyncPlan(IReadOnlyList<EventCreation> Creations, IReadOnlyList<EventChange> Changes, IReadOnlyList<Guid> Removals, int HeldRemovals)
{
    public int Total => Creations.Count + Changes.Count + Removals.Count;
}

/// <summary>
/// 比對 GD 的期望集合與已記錄的列，決定新增、修改、救回與移除（純函式，不碰資料庫與 Google）。
/// 宣告式的比對讓每一輪都自我修正：漏掉的一輪、失敗的推送、手動在 Google 刪掉的事件，下一輪都會回到 GD 的狀態，不像舊系統依賴每一步都成功。
/// </summary>
public static class CalendarSyncPlanner
{
    public static CalendarSyncPlan Plan(IReadOnlyList<AbsenceSegment> desired, IReadOnlyList<KnownEvent> known, double holdRatio, int holdMinimum, bool confirmRemovals)
    {
        var rows = known.ToDictionary(x => (x.DesNo, x.SegmentKey));
        var creations = new List<EventCreation>();
        var changes = new List<EventChange>();
        var wanted = new HashSet<(string, string)>();
        foreach (var segment in desired)
        {
            wanted.Add(segment.Key);
            var hash = CalendarEventContent.Hash(segment);
            if (!rows.TryGetValue(segment.Key, out var row)) creations.Add(new(segment, hash));
            else if (row.State == CalendarEventStates.Removed || !row.ContentHash.AsSpan().SequenceEqual(hash)) changes.Add(new(row.Id, segment, hash));
        }

        var removals = known.Where(x => x.InWindow && x.State != CalendarEventStates.Removed && !wanted.Contains((x.DesNo, x.SegmentKey))).Select(x => x.Id).ToList();
        var active = known.Count(x => x.InWindow && x.State != CalendarEventStates.Removed);
        var hold = !confirmRemovals && removals.Count >= holdMinimum && removals.Count > active * holdRatio;
        return hold ? new(creations, changes, [], removals.Count) : new(creations, changes, removals, 0);
    }
}
