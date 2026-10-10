using AiNexus.Platform.Errors;

namespace AiNexus.Features.Monitoring;

internal static class MonitoringErrors
{
    public static readonly Error InvalidWindow = Error.Invalid("invalid_request");
    public static readonly Error StreamLimit = Error.RateLimited("rate_limited");
    public static readonly Error FeatureForbidden = Error.Forbidden("feature_forbidden");
    public static readonly Error SessionRevoked = Error.Unauthenticated("session_revoked");
}
