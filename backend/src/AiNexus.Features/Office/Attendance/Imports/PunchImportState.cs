namespace AiNexus.Features.Office.Attendance.Imports;

/// <summary>
/// 來源資料夾讀不到的狀態，供監控判為 failing：這種失敗沒有檔案可記，不會出現在匯入紀錄裡。只存在記憶體，重啟後歸零，所以註冊為單例。
/// 與 <see cref="Admin.Integrations.Gd.GdAccess"/> 一樣只在狀態轉換時回報，讓掃描只記一次日誌，不在中斷期間每個週期洗版。
/// </summary>
public sealed class PunchImportState
{
    private string? sourceError;

    /// <summary>最近一次掃描讀不到來源時的例外型別名稱；null 表示讀得到（或還沒掃過）。</summary>
    public string? SourceError => Volatile.Read(ref sourceError);

    /// <summary>回傳 true 表示這次由讀得到轉為讀不到。</summary>
    public bool SourceFailed(string errorType) => Interlocked.Exchange(ref sourceError, errorType) is null;

    /// <summary>回傳 true 表示先前讀不到、現在恢復。</summary>
    public bool SourceRead() => Interlocked.Exchange(ref sourceError, null) is not null;
}
