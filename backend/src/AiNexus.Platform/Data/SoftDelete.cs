namespace AiNexus.Platform.Data;

/// <summary>
/// EF Core 軟刪除全域查詢篩選器名稱。
/// 實體於模型配置中宣告 <c>HasQueryFilter(SoftDelete.Filter, x =&gt; !x.IsDeleted)</c>；
/// 需要檢視已刪除資料的特定查詢（如管理者後台讀取、過期清理工作、或背景任務繼續處理被刪除實體）可透過
/// <c>IgnoreQueryFilters([SoftDelete.Filter])</c> 針對性略過此篩選器，而保留其他篩選條件。
/// </summary>
public static class SoftDelete
{
    public const string Filter = "SoftDelete";
}
