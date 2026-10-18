using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Sharing;

internal static class SharingErrors
{
    public const string InvalidCode = "share_invalid";
    public static readonly Error RecipientUnknown = Error.Invalid("share_recipient_unknown");
    public static readonly Error LimitReached = Error.Conflict("share_limit");
    public static readonly Error GenerationActive = Error.Conflict("share_generation_active");
    public static readonly Error HistoryInvalid = Error.Conflict("share_history_invalid");
    public static readonly Error HistoryLimit = Error.Conflict("share_history_limit");
    public static readonly Error ContentLimit = Error.Conflict("share_content_limit");

    /// <summary>遺失、過期、已撤銷與非寄給自己的分享一律回傳同一個結果，讓收件人無法探測其他分享。</summary>
    public static readonly Error Unavailable = Error.NotFound("share_unavailable");
}
