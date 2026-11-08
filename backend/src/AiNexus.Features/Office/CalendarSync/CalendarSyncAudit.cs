using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Office.CalendarSync;

/// <summary>日曆同步的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class CalendarSyncAudit
{
    /// <summary>事件清單列出誰在什麼時候請假、出差，屬於個人資料的查閱。</summary>
    public static readonly AuditAction EventRead = AuditAction.Access("calendar-sync.event.read", "查詢日曆同步事件");
    public static readonly AuditAction RunRead = AuditAction.Access("calendar-sync.run.read", "查詢日曆同步紀錄");

    /// <summary>一輪的明細列出哪些人的假單被同步或移除，屬於個人資料的查閱。</summary>
    public static readonly AuditAction RunItemRead = AuditAction.Access("calendar-sync.item.read", "查詢日曆同步明細");
    public static readonly AuditAction RunRequested = AuditAction.Administration("calendar-sync.run.requested", "手動同步 Google 日曆");
    public static readonly AuditAction ResetRequested = AuditAction.Administration("calendar-sync.reset.requested", "要求重設 Google 日曆");
    public static readonly AuditAction ConnectionTested = AuditAction.Administration("calendar-sync.connection.verified", "測試 Google 日曆連線");

    /// <summary>一輪同步改了 Google 日曆（建立、更新或刪除事件；系統動作，擁有者為空）；沒有異動的一輪不記。</summary>
    public static readonly AuditAction Synced = AuditAction.Administration("calendar-sync.run.synced", "同步 Google 日曆");

    /// <summary>重設完成：日曆上本系統記錄的事件已刪除，事件列與同步紀錄已清除（系統動作，擁有者為空）。</summary>
    public static readonly AuditAction Cleared = AuditAction.Administration("calendar-sync.calendar.cleared", "重設 Google 日曆");
}
