using System.Security.Claims;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Users;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Validation;
using FluentValidation;
using AiNexus.Features.Identity.Sessions;

namespace AiNexus.Features.Monitoring;

public sealed record PresenceRequest(Guid SessionId, string Feature, string State);

internal sealed class PresenceRequestValidator : RequestValidator<PresenceRequest>
{
    public override string ProblemCode => "invalid_request";

    public PresenceRequestValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Feature).Must(MonitoringVocabulary.ValidFeature).WithErrorCode("unknown");
        RuleFor(x => x.State).Must(x => x is "active" or "idle" or "background").WithErrorCode("unknown");
    }
}
public sealed record PresenceReceipt(bool Enabled, int HeartbeatSeconds);

/// <summary>
/// A browser tab's heartbeat: the user, coarse browser and device names, the address and a fixed feature name. Raw user
/// agents, URLs and titles never enter telemetry. Rate limited and kept out of the request log.
/// </summary>
internal sealed class HeartbeatPresence(RuntimeTraffic traffic)
{
    public static void Map(RouteGroupBuilder api) => api
        .MapPost("/presence", (PresenceRequest request, HttpContext http, ICurrentUser user, HeartbeatPresence handler) => TypedResults.Ok(handler.Handle(http, user.User, request)))
        .RequireRateLimiting(MonitoringModule.PresenceRateLimit).WithMetadata(new SuppressSuccessfulRequestLog()).WithName("HeartbeatPresence");

    public PresenceReceipt Handle(HttpContext http, NexusUser user, PresenceRequest request)
    {
        var ua = http.Request.Headers.UserAgent.ToString();
        var browser = ua.Contains("Edg/", StringComparison.Ordinal) ? "Edge" : ua.Contains("Firefox/", StringComparison.Ordinal) ? "Firefox" : ua.Contains("Chrome/", StringComparison.Ordinal) ? "Chrome" : ua.Contains("Safari/", StringComparison.Ordinal) ? "Safari" : "其他瀏覽器";
        var device = ua.Contains("Android", StringComparison.Ordinal) ? "Android" : ua.Contains("iPhone", StringComparison.Ordinal) || ua.Contains("iPad", StringComparison.Ordinal) ? "iOS" : ua.Contains("Windows", StringComparison.Ordinal) ? "Windows" : ua.Contains("Mac", StringComparison.Ordinal) ? "macOS" : ua.Contains("Linux", StringComparison.Ordinal) ? "Linux" : "未知裝置";
        var ip = http.Connection.RemoteIpAddress; if (ip?.IsIPv4MappedToIPv6 == true) ip = ip.MapToIPv4();
        traffic.Presence(user, request, ip?.ToString() ?? "未知", browser, device,
            http.User.FindFirstValue(SessionIdentity.Method) is "local" ? "local" : "ad", http.User.HasClaim(x => x.Type == SessionIdentity.ActorId));
        return new PresenceReceipt(traffic.Enabled, 25);
    }
}
