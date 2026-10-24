namespace AiNexus.Features.Office.Attendance.Leave;

/// <summary>
/// 假單查詢結果。<paramref name="Available"/> 為 false 表示公文系統連不上，此時其他欄位都是空的，呼叫端要標示「假單無法取得」，不能當成沒請假。
/// <paramref name="Sheets"/> 只含有單據的使用者，依開始時間排序；<paramref name="Unmapped"/> 的人對不到公文帳號，假單同樣是未知。
/// </summary>
public sealed record LeaveLookup(bool Available, IReadOnlyDictionary<Guid, IReadOnlyList<LeaveSheet>> Sheets, IReadOnlySet<Guid> Unmapped)
{
    public static LeaveLookup Unavailable { get; } = new(false, new Dictionary<Guid, IReadOnlyList<LeaveSheet>>(), new HashSet<Guid>());

    /// <summary>該使用者的單據，沒有時為空清單。</summary>
    public IReadOnlyList<LeaveSheet> For(Guid userId) => Sheets.TryGetValue(userId, out var sheets) ? sheets : [];
}
