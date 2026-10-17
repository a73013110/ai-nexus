namespace AiNexus.Platform.Data;

/// <summary>
/// EF Core 軟刪除全域查詢篩選器名稱。
/// 實體在模型配置中以 <c>HasQueryFilter(SoftDelete.Filter, x =&gt; !x.IsDeleted)</c> 宣告。
/// 需要看見已刪除資料的查詢（如管理後台、清理工作，或背景任務要繼續處理已刪除的實體）才用
/// <c>IgnoreQueryFilters([SoftDelete.Filter])</c> 精準略過此篩選器，其餘篩選條件仍然保留。
/// </summary>
public static class SoftDelete
{
    public const string Filter = "SoftDelete";
}
