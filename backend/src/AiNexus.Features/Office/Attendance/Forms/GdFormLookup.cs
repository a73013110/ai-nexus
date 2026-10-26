namespace AiNexus.Features.Office.Attendance.Forms;

/// <summary>
/// 表單查詢結果。<paramref name="Available"/> 為 false 表示公文系統連不上，此時其他欄位都是空的，呼叫端要標示「表單無法取得」，不能當成沒請假。
/// <paramref name="Forms"/> 只含有表單的使用者，依開始時間排序；<paramref name="Unmapped"/> 的人對不到公文帳號，假單同樣是未知。
/// </summary>
public sealed record GdFormLookup(bool Available, IReadOnlyDictionary<Guid, IReadOnlyList<GdForm>> Forms, IReadOnlySet<Guid> Unmapped)
{
    public static GdFormLookup Unavailable { get; } = new(false, new Dictionary<Guid, IReadOnlyList<GdForm>>(), new HashSet<Guid>());

    /// <summary>該使用者的表單，沒有時為空清單。</summary>
    public IReadOnlyList<GdForm> For(Guid userId) => Forms.TryGetValue(userId, out var forms) ? forms : [];
}
