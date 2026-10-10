using AiNexus.Features.Identity;
using AiNexus.Platform.Diagnostics;

namespace AiNexus.Features.Monitoring;

/// <summary>A closing tab leaves the live session list at once instead of timing out.</summary>
internal sealed class LeavePresence(RuntimeTraffic traffic)
{
    public static void Map(RouteGroupBuilder api) => api
        .MapDelete("/presence/{sessionId:guid}", (Guid sessionId, ICurrentUser user, LeavePresence handler) =>
        {
            handler.Handle(user.Id, sessionId);
            return TypedResults.NoContent();
        }).WithMetadata(new SuppressSuccessfulRequestLog()).WithName("LeavePresence");

    public void Handle(Guid user, Guid sessionId) => traffic.Leave(user, sessionId);
}
