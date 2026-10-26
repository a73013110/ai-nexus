using AiNexus.Platform.Errors;

namespace AiNexus.Features.Office.Attendance;

/// <summary>出勤模組的預期失敗。</summary>
public static class AttendanceErrors
{
    public static readonly Error CardNotFound = Error.NotFound("attendance_card_not_found");
    public static readonly Error CardNumberDuplicate = Error.Conflict("attendance_card_number_duplicate");

    /// <summary>
    /// 有刷卡或持有期間的卡不能刪除：持有期間是歸戶的依據，刷卡則會在下次匯入又建回這個卡號。只刪手動建立錯誤、從沒用過的卡；
    /// 不想在出勤紀錄看到的卡改為隱藏。
    /// </summary>
    public static readonly Error CardInUse = Error.Conflict("attendance_card_in_use");

    public static readonly Error AssignmentNotFound = Error.NotFound("attendance_assignment_not_found");

    /// <summary>不存在或已刪除的使用者不能成為持有人。</summary>
    public static readonly Error UserNotFound = Error.NotFound("attendance_user_not_found");

    /// <summary>新段開始時間落在該卡有人持有的期間內，而呼叫端沒有明確要求接手；前端提示後以接手旗標重送。</summary>
    public static readonly Error CardHeld = Error.Conflict("attendance_card_held");

    /// <summary>期間與同一張卡的其他持有期間重疊，而且接手也無法解決（例如新期間落在舊期間中間，會把舊期間切成兩段）。</summary>
    public static readonly Error AssignmentOverlap = Error.Conflict("attendance_assignment_overlap");

    /// <summary>新段開始時間不晚於它重疊的既有持有期間的開始時間；接手只會結束前一段的尾端，這種情況不論有沒有旗標都不能處理。</summary>
    public static readonly Error StartsNotAfterPrevious = Error.Conflict("attendance_assignment_starts_not_after_previous");

    /// <summary>查詢條件不合法：卡片狀態不明，或查持有歷史時沒有指定卡片或使用者（不開放整張表）。</summary>
    public static readonly Error FilterInvalid = Error.Invalid("attendance_filter_invalid");

    /// <summary>出勤紀錄的查詢區間不合法：起訖必填、結束不早於開始、最長一年（見 <c>AttendanceRecordsFilter</c>）。</summary>
    public static readonly Error RecordsRangeInvalid = Error.Invalid("attendance_records_range_invalid");

    /// <summary>出勤紀錄的其他條件不合法：持有人姓名過長、<c>offset</c> 為負，或每頁人數不在 1～50。</summary>
    public static readonly Error RecordsFilterInvalid = Error.Invalid("attendance_records_filter_invalid");

    /// <summary>匯入紀錄的查詢條件不合法：狀態不明、期間顛倒、每頁筆數不在允許值內，或游標不存在。</summary>
    public static readonly Error ImportFilterInvalid = Error.Invalid("attendance_import_filter_invalid");

    /// <summary>來源資料夾裡沒有指定日期的打卡檔，區間內一個都沒有（打卡機尚未輸出，或檔名日期錯誤）；只缺部分檔案不是錯誤。</summary>
    public static readonly Error ImportFileNotFound = Error.NotFound("attendance_import_file_not_found");

    /// <summary>來源資料夾讀不到（網路共用中斷、IIS 集區身分沒有權限）；此時沒有檔案可記錄，所以不產生匯入紀錄。</summary>
    public static readonly Error ImportSourceUnavailable = Error.Unavailable("attendance_import_source_unavailable");

    /// <summary>公文系統（GD）未設定、連不上、逾時或拒絕存取；必須讀寫 GD 才能完成的請求回這個，不回假的空結果。</summary>
    public static readonly Error GdUnavailable = Error.Unavailable("attendance_gd_unavailable");

    /// <summary>
    /// <c>Databases:Gd</c> 設為 <c>ReadOnly=true</c>：部署選擇只讀 GD，假日只能查詢、回寫停用。
    /// 與其他「依設定停用」的功能一樣回 503；它不會自己恢復，要改設定並重啟，所以與暫時連不上的 <see cref="GdUnavailable"/> 分開。
    /// </summary>
    public static readonly Error GdReadOnly = Error.Unavailable("attendance_gd_read_only");

    /// <summary>
    /// 操作者對不到唯一的公文帳號（沒有 AD 帳號、<c>Sys_User</c> 沒有，或對到多筆）。回寫假日要記修改者的 <c>Sys_User.ID</c>，
    /// 對不到時拒絕，不退回寫 <c>sys</c>，否則 GD 那邊看不出是誰改的。
    /// </summary>
    public static readonly Error GdAccountUnmapped = Error.Forbidden("attendance_gd_account_unmapped");

    /// <summary>
    /// 表單不存在、不是流程中或核准的單，或申請人不在看的人的出勤紀錄範圍內。三者不分開回報：分開等於讓人以單號試出別人有沒有請假。
    /// </summary>
    public static readonly Error GdFormNotFound = Error.NotFound("attendance_gd_form_not_found");

    /// <summary>假日查詢區間不合法：起訖必填、結束不早於開始、不超過一年（GD 是別人的系統，不開放整張表）。</summary>
    public static readonly Error CalendarRangeInvalid = Error.Invalid("attendance_calendar_range_invalid");

    /// <summary>GD 沒有這一天；回寫只更新既有日期，不新增，日期列由公文系統建立。</summary>
    public static readonly Error CalendarDayNotFound = Error.NotFound("attendance_calendar_day_not_found");

    /// <summary>備註以 GD 欄位的字碼頁存不下：超過 255 位元組，或含該字碼頁沒有的字（會被換成問號）。</summary>
    public static readonly Error CalendarRemarkInvalid = Error.Invalid("attendance_calendar_remark_invalid");
}
