using AiNexus.Platform.Errors;

namespace AiNexus.Features.Monitoring;

internal static class MonitoringErrors
{
    public static readonly Error InvalidWindow = Error.Invalid("invalid_request");
    public static readonly Error StreamSuperseded = Error.Conflict("stream_superseded");
    public static readonly Error SessionRevoked = Error.Unauthenticated("session_revoked");
}
