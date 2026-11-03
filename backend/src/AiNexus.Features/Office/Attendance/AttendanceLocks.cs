namespace AiNexus.Features.Office.Attendance;

/// <summary>出勤模組的應用程式鎖：只保護資料庫約束擋不住的規則，其餘併發由唯一索引與 rowversion 處理。</summary>
public static class AttendanceLocks
{
    /// <summary>一次一個匯入寫入。新增與刪除卡片也要取：匯入會自動建卡，撞上唯一索引時整批匯入都要回滾；也可能正把刷卡寫到要刪的卡號（刷卡與卡片之間沒有外鍵）。</summary>
    public const string PunchImport = "attendance:punch-import";

    /// <summary>一張卡的持有期間不可重疊：規則跨列又沒有約束能擋，所以同一張卡的寫入排隊，不同卡互不等待。</summary>
    public static string Card(Guid id) => $"attendance:card:{id}";
}
