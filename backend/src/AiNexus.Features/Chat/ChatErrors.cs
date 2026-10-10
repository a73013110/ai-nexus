using AiNexus.Platform.Errors;

namespace AiNexus.Features.Chat;

internal static class ChatErrors
{
    public static readonly Error SchedulerUnavailable = Error.Unavailable("scheduler_unavailable");
    public static readonly Error InvalidHistory = Error.Conflict("invalid_history");
    public static readonly Error AttachmentMissing = Error.Conflict("attachment_not_found");
    public static readonly Error AccessRevoked = Error.Forbidden("chat_access_revoked");
    public static readonly Error SubscriptionLimit = Error.RateLimited("subscription_limit");
    public static readonly Error InvalidCursor = Error.Invalid("invalid_cursor");
    public static readonly Error CursorAhead = Error.Conflict("invalid_cursor");
}
