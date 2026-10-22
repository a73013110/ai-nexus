namespace AiNexus.Features.Staff.Attendance.Imports;

/// <summary>
/// 同一時間只讓一個匯入執行（排程與 HR 手動重匯共用）。不是每個檔案各一把鎖：跨檔去重與重匯時的鄰檔補救（見 <see cref="PunchImporter"/>）
/// 會讀寫別的檔案的列，兩個檔案同時匯入也可能搶同一筆刷卡的唯一鍵。匯入每天只有個位數、各自只要幾秒，序列化沒有代價。
/// 只涵蓋單一程序（本站只有一個 IIS 執行個體）；多程序重疊時由 Punches 的唯一索引擋下，後到的匯入記為失敗並由排程重試。
/// </summary>
public sealed class PunchImportGate
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
}
