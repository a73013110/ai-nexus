namespace AiNexus.Data.App.Identity;

/// <summary>
/// 依人分組的列表（出勤紀錄、匯出、人員選單）共用的使用者順序：有 <see cref="User.SortOrder"/> 的在前、由小到大，
/// 沒有的在後；同值再依姓名、帳號。只有姓名、沒有帳號的資料視為未排序。
/// </summary>
public static class UserOrder
{
    /// <summary>排序值上限（含）；與資料庫 <c>ck_Users_SortOrder</c> 相同。值之間預設間隔 10，一萬人以上仍放得下。</summary>
    public const int MaxSortOrder = 1_000_000;

    /// <summary>
    /// 在記憶體排序（姓名用 ordinal，結果不受伺服器文化設定影響）。呼叫端可再接 <c>ThenBy</c> 讓同名同序的列穩定。
    /// </summary>
    public static IOrderedEnumerable<T> InUserOrder<T>(this IEnumerable<T> source, Func<T, int?> sortOrder, Func<T, string> displayName, Func<T, string?> account)
        => source.OrderBy(x => sortOrder(x) is null).ThenBy(sortOrder).ThenBy(displayName, StringComparer.Ordinal).ThenBy(account, StringComparer.OrdinalIgnoreCase);
}
