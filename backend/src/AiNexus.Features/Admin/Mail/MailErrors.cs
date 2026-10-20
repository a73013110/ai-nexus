using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Mail;

/// <summary>
/// 郵件的預期失敗。寄送結果的分類代碼（<c>mail_rejected</c> 等）不是 API 錯誤，只寫在寄送紀錄，見 <c>Delivery/MailFailures</c>。
/// </summary>
public static class MailErrors
{
    public static readonly Error AddressInvalid = Error.Invalid("mail_address_invalid");
    public static readonly Error RecipientBlocked = Error.Invalid("mail_recipient_blocked");
    public static readonly Error NotConfigured = Error.Unavailable("mail_not_configured");
    public static readonly Error MessageNotFound = Error.NotFound("mail_message_not_found");
    /// <summary>敏感信或內文已依保存期限刪除，不能重寄。</summary>
    public static readonly Error ContentUnavailable = Error.Conflict("mail_content_unavailable");
    /// <summary>收件原址已依保存期限清除，不能重寄。</summary>
    public static readonly Error RecipientUnavailable = Error.Conflict("mail_recipient_unavailable");
    /// <summary>只有等待中的信可以立即重試；已結束的信要重寄。</summary>
    public static readonly Error NotRetryable = Error.Conflict("mail_not_retryable");
    public static readonly Error FilterInvalid = Error.Invalid("mail_filter_invalid");
    public static readonly Error CursorInvalid = Error.Invalid("mail_cursor_invalid");
    /// <summary>寄信測試選的範本沒有登記（<see cref="MailSample"/>）。</summary>
    public static readonly Error SampleNotFound = Error.NotFound("mail_sample_not_found");
    /// <summary>寄信測試的參數有未知名稱，或值不符合型別。</summary>
    public static readonly Error SampleInvalid = Error.Invalid("mail_sample_invalid");
}
