using AiNexus.Platform.Errors;

namespace AiNexus.Features.Sharing;

internal static class SharingErrors
{
    public const string InvalidCode = "share_invalid";
    public static readonly Error RecipientUnknown = Error.Invalid("share_recipient_unknown");
    public static readonly Error LimitReached = Error.Conflict("share_limit");
    public static readonly Error GenerationActive = Error.Conflict("share_generation_active");
    public static readonly Error HistoryInvalid = Error.Conflict("share_history_invalid");
    public static readonly Error HistoryLimit = Error.Conflict("share_history_limit");
    public static readonly Error ContentLimit = Error.Conflict("share_content_limit");

    /// <summary>One answer for missing, expired, revoked and not-addressed-to-you, so recipients cannot probe other shares.</summary>
    public static readonly Error Unavailable = Error.NotFound("share_unavailable");
}
