using AiNexus.Platform.Errors;

namespace AiNexus.Features.Platform.Notifications;

internal static class NotificationsErrors
{
    public static readonly Error CursorInvalid = Error.Invalid("notification_cursor_invalid");
    public static readonly Error CursorRequired = Error.Invalid("notification_cursor_required");
}
