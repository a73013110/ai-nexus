using AiNexus.Features.Identity;
using AiNexus.Platform.Diagnostics;

namespace AiNexus.Features.Monitoring;

/// <summary>A closing tab leaves the live session list at once instead of timing out.</summary>
internal static class LeavePresence
{
    public static void Map(RouteGroupBuilder api) => api
        .MapDelete("/presence/{sessionId:guid}", (Guid sessionId, ICurrentUser user, RuntimeTraffic traffic) => {
            traffic.Leave(user.Id, sessionId); return Results.NoContent();
        }).WithMetadata(new SuppressSuccessfulRequestLog()).WithName("LeavePresence");
}
