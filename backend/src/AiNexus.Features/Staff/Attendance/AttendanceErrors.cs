using AiNexus.Platform.Errors;

namespace AiNexus.Features.Staff.Attendance;

/// <summary>出勤模組的預期失敗。</summary>
public static class AttendanceErrors
{
    public static readonly Error CardNotFound = Error.NotFound("attendance_card_not_found");
    public static readonly Error CardNumberDuplicate = Error.Conflict("attendance_card_number_duplicate");

    /// <summary>已有持有期間的卡不能刪除、也不能改成訪客卡：歷史會斷掉，或讓既有的歸戶靜默失效。</summary>
    public static readonly Error CardHasAssignments = Error.Conflict("attendance_card_has_assignments");

    /// <summary>訪客卡不歸戶（view 只看員工卡的持有期間），指派了也不會生效，所以直接拒絕。</summary>
    public static readonly Error CardIsVisitor = Error.Conflict("attendance_card_visitor");

    public static readonly Error AssignmentNotFound = Error.NotFound("attendance_assignment_not_found");

    /// <summary>不存在或已刪除的使用者不能成為持有人。</summary>
    public static readonly Error UserNotFound = Error.NotFound("attendance_user_not_found");

    /// <summary>
    /// 新段開始時間落在該卡目前仍有人持有的期間內，而呼叫端沒有明確要求接手；回應帶目前的持有期間（<c>CardHeldResponse</c>），
    /// 前端提示後以接手旗標重送。
    /// </summary>
    public static readonly Error CardHeld = Error.Conflict("attendance_card_held");

    /// <summary>期間與同一張卡的其他持有期間重疊，而且接手也無法解決（例如新期間落在舊期間中間，會把舊期間切成兩段）。</summary>
    public static readonly Error AssignmentOverlap = Error.Conflict("attendance_assignment_overlap");

    /// <summary>新段開始時間不晚於它重疊的既有持有期間的開始時間；接手只會結束前一段的尾端，這種情況不論有沒有旗標都不能處理。</summary>
    public static readonly Error StartsNotAfterPrevious = Error.Conflict("attendance_assignment_starts_not_after_previous");

    /// <summary>查詢條件不合法：種類不明，或查持有歷史時沒有指定卡片或使用者（不開放整張表）。</summary>
    public static readonly Error FilterInvalid = Error.Invalid("attendance_filter_invalid");

    public static readonly Error PeriodInvalid = Error.Invalid("attendance_period_invalid");
}
