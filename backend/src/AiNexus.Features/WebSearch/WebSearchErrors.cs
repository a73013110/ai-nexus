using AiNexus.Platform.Errors;

namespace AiNexus.Features.WebSearch;

internal static class WebSearchErrors
{
    public static readonly Error NotConfigured = Error.Unavailable("web_search_not_configured");
    public static readonly Error QueryTooLong = Error.Invalid("web_query_too_long");
    public static readonly Error IdempotencyConflict = Error.Conflict("idempotency_conflict");
    public static readonly Error PendingOrFailed = Error.Conflict("web_search_pending_or_failed");
    public static readonly Error DailyQuota = Error.RateLimited("web_search_daily_quota");
    public static readonly Error Unavailable = Error.Unavailable("web_search_unavailable");
    public static readonly Error RateLimited = Error.RateLimited("web_search_unavailable");
    public static readonly Error Timeout = Error.Timeout("web_search_timeout");
}
