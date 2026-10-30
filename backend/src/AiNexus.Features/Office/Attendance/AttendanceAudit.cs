using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Office.Attendance;

/// <summary>出勤模組的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class AttendanceAudit
{
    public static readonly AuditAction RecordRead = AuditAction.Access("attendance.record.read", "查詢出勤紀錄");
    public static readonly AuditAction RecordExported = AuditAction.Access("attendance.record.exported", "匯出出勤紀錄");
    public static readonly AuditAction FormRead = AuditAction.Access("attendance.form.read", "檢視假單明細");

    public static readonly AuditAction CardRead = AuditAction.Access("attendance.card.read", "查詢卡片");
    public static readonly AuditAction CardCreated = AuditAction.Administration("attendance.card.created", "新增卡片");
    public static readonly AuditAction CardUpdated = AuditAction.Administration("attendance.card.updated", "修改卡片");
    public static readonly AuditAction CardDeleted = AuditAction.Administration("attendance.card.deleted", "刪除卡片");
    public static readonly AuditAction PunchRead = AuditAction.Access("attendance.punch.read", "查詢刷卡紀錄");
    public static readonly AuditAction AssignmentRead = AuditAction.Access("attendance.card_assignment.read", "查詢卡片持有期間");
    public static readonly AuditAction AssignmentCreated = AuditAction.Administration("attendance.card_assignment.created", "新增卡片持有期間");
    public static readonly AuditAction AssignmentUpdated = AuditAction.Administration("attendance.card_assignment.updated", "修改卡片持有期間");
    public static readonly AuditAction AssignmentDeleted = AuditAction.Administration("attendance.card_assignment.deleted", "刪除卡片持有期間");

    public static readonly AuditAction ImportRead = AuditAction.Access("attendance.punch_import.read", "查詢打卡匯入紀錄");
    public static readonly AuditAction PunchFileImported = AuditAction.Administration("attendance.punch_file.imported", "手動重匯打卡檔");
    public static readonly AuditAction CalendarDayRead = AuditAction.Access("attendance.calendar_day.read", "查詢假日設定");
    public static readonly AuditAction CalendarDayUpdated = AuditAction.Administration("attendance.calendar_day.updated", "修改假日設定");
}
